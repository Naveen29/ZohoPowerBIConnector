using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ZohoController : ControllerBase
{
    private const string DefaultZohoBaseUrl = "https://www.zohoapis.com/books/v3";

    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;

    public ZohoController(ApplicationDbContext dbContext, IDataProtectionProvider protectionProvider, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _protector = protectionProvider.CreateProtector("ZohoSecretsProtector");
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var config = await _dbContext.ZohoConfigurations.Where(z => z.UserId == user.Id).FirstOrDefaultAsync();
        if (config is null)
        {
            return Ok(new { exists = false, clientId = string.Empty, baseUrl = DefaultZohoBaseUrl, organizationId = string.Empty, clientSecret = string.Empty, refreshToken = string.Empty, status = "Not configured", lastValidated = "Never" });
        }

        var clientSecret = string.Empty;
        var refreshToken = string.Empty;
        try
        {
            clientSecret = _protector.Unprotect(config.ClientSecret);
            refreshToken = _protector.Unprotect(config.RefreshToken);
        }
        catch
        {
            // Ignore decryption issues and return empty secrets.
        }

        return Ok(new
        {
            exists = true,
            clientId = config.ClientId,
            baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? DefaultZohoBaseUrl : config.BaseUrl,
            organizationId = config.OrganizationId,
            clientSecret,
            refreshToken,
            status = config.LastSyncAt.HasValue ? "Validated" : "Configured",
            lastValidated = config.LastSyncAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"
        });
    }

    [HttpPost("config")]
    public async Task<IActionResult> SaveConfig([FromBody] ZohoConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.OrganizationId) || string.IsNullOrWhiteSpace(request.ClientSecret) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "ClientId, OrganizationId, ClientSecret and RefreshToken are required." });
        }

        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var configs = await _dbContext.ZohoConfigurations.Where(z => z.UserId == user.Id).ToListAsync();
        var config = configs.FirstOrDefault();
        if (config is null)
        {
            config = new ZohoConfiguration { UserId = user.Id };
            _dbContext.ZohoConfigurations.Add(config);
        }
        else if (configs.Count > 1)
        {
            _dbContext.ZohoConfigurations.RemoveRange(configs.Skip(1));
        }

        config.ClientId = request.ClientId.Trim();
        config.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
            ? (string.IsNullOrWhiteSpace(config.BaseUrl) ? DefaultZohoBaseUrl : config.BaseUrl)
            : request.BaseUrl.Trim();
        config.OrganizationId = request.OrganizationId.Trim();
        config.ClientSecret = _protector.Protect(request.ClientSecret.Trim());
        config.RefreshToken = _protector.Protect(request.RefreshToken.Trim());
        config.LastSyncAt ??= null;

        await _dbContext.SaveChangesAsync();

        // After saving configuration, ensure the user has at least a Free plan assigned.
        var now = DateTime.UtcNow;
        var activeSub = await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == user.Id && s.IsActive && s.ExpiryDate >= now)
            .OrderByDescending(s => s.ExpiryDate)
            .FirstOrDefaultAsync();

        string? assignedPlan = activeSub?.Plan?.Name;
        bool paymentRequired = false;
        bool paymentComingSoon = false;

        if (request.PlanId.HasValue)
        {
            var requestedPlan = await _dbContext.Plans.FindAsync(request.PlanId.Value);
            if (requestedPlan is null || !requestedPlan.IsActive)
            {
                return BadRequest(new { error = "Requested plan not found or inactive." });
            }

            if (requestedPlan.Price <= 0m)
            {
                // Assign free (or zero-priced) plan immediately
                var start = DateTime.UtcNow;
                var expiry = start.AddDays(requestedPlan.DurationDays);
                var subscription = new Subscription
                {
                    UserId = user.Id,
                    PlanId = requestedPlan.Id,
                    StartDate = start,
                    ExpiryDate = expiry,
                    IsActive = true
                };

                _dbContext.Subscriptions.Add(subscription);
                await _dbContext.SaveChangesAsync();
                assignedPlan = requestedPlan.Name;
            }
            else
            {
                // Paid plan requested: payment flow not yet implemented
                paymentRequired = true;
                paymentComingSoon = true;
            }
        }
        else
        {
            // If no plan requested and none active, assign default Free plan.
            if (activeSub is null)
            {
                var freePlan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.IsActive && p.Price <= 0m && p.Name == "Free");
                if (freePlan is not null)
                {
                    var start = DateTime.UtcNow;
                    var expiry = start.AddDays(freePlan.DurationDays);
                    var subscription = new Subscription
                    {
                        UserId = user.Id,
                        PlanId = freePlan.Id,
                        StartDate = start,
                        ExpiryDate = expiry,
                        IsActive = true
                    };

                    _dbContext.Subscriptions.Add(subscription);
                    await _dbContext.SaveChangesAsync();
                    assignedPlan = freePlan.Name;
                }
            }
        }

        return Ok(new
        {
            message = "Zoho configuration saved successfully.",
            baseUrl = config.BaseUrl,
            organizationId = config.OrganizationId,
            clientId = config.ClientId,
            assignedPlan,
            paymentRequired,
            paymentComingSoon
        });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateConnection([FromBody] ZohoValidateRequest? request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var config = await _dbContext.ZohoConfigurations.Where(z => z.UserId == user.Id).FirstOrDefaultAsync();
        if (config is null && (request is null || string.IsNullOrWhiteSpace(request.ClientId)))
        {
            return BadRequest(new { error = "Zoho configuration is missing." });
        }

        var clientId = request?.ClientId?.Trim() ?? config?.ClientId;
        var baseUrl = request?.BaseUrl?.Trim();
        var organizationId = request?.OrganizationId?.Trim() ?? config?.OrganizationId;
        var clientSecret = request?.ClientSecret?.Trim();
        var refreshToken = request?.RefreshToken?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultZohoBaseUrl;
        }

        if (config is not null && string.IsNullOrWhiteSpace(clientSecret))
        {
            try
            {
                clientSecret = _protector.Unprotect(config.ClientSecret);
                refreshToken = _protector.Unprotect(config.RefreshToken);
            }
            catch
            {
                return BadRequest(new { error = "Unable to decrypt Zoho credentials." });
            }
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new { error = "Valid Zoho configuration values are required for validation." });
        }

        var tokenResult = await RefreshZohoAccessTokenAsync(baseUrl, clientId, clientSecret, refreshToken);
        if (!tokenResult.Success)
        {
            return BadRequest(new { error = tokenResult.Error });
        }

        var validationResult = await ValidateZohoApiAsync(baseUrl, organizationId, tokenResult.AccessToken!);
        if (!validationResult.Success)
        {
            return BadRequest(new { error = validationResult.Error });
        }

        var now = DateTime.UtcNow;
        if (config is not null)
        {
            config.LastSyncAt = now;
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { status = "Connection validated", clientId, baseUrl, organizationId, lastValidated = now.ToString("yyyy-MM-dd HH:mm") });
    }

    private async Task<(bool Success, string? Error, string? AccessToken)> RefreshZohoAccessTokenAsync(string baseUrl, string clientId, string clientSecret, string refreshToken)
    {
        var accountsHost = GetZohoAccountsHost(baseUrl);

        try
        {
            var client = _httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "refresh_token"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://{accountsHost}/oauth/v2/token"))
            {
                Content = new FormUrlEncodedContent(form)
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // If Zoho returned HTML (accounts error page), give a clearer message
                if (!string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("<"))
                {
                    return (false, $"Zoho token refresh failed: received HTML from accounts service (status {response.StatusCode}). Ensure the refresh token and client credentials are correct and use the correct Zoho region.", null);
                }

                var error = TryExtractZohoError(content) ?? content;
                return (false, $"Zoho token refresh failed: {error}", null);
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                if (!document.RootElement.TryGetProperty("access_token", out var accessTokenElement))
                {
                    return (false, "Zoho token refresh response did not include access_token.", null);
                }

                return (true, null, accessTokenElement.GetString());
            }
            catch (JsonException)
            {
                return (false, "Unable to parse Zoho token response.", null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Zoho token refresh exception: {ex.Message}", null);
        }
    }

    private async Task<(bool Success, string? Error)> ValidateZohoApiAsync(string baseUrl, string organizationId, string accessToken)
    {
        var apiBaseUrl = baseUrl.TrimEnd('/');
        var organizationsEndpoint = $"{apiBaseUrl}/organizations?organization_id={Uri.EscapeDataString(organizationId)}";
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, organizationsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var error = TryExtractZohoError(content) ?? content;
            return (false, $"Zoho API validation failed: {error}");
        }

        return (true, null);
    }

    private static string? TryExtractZohoError(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error_description", out var description))
            {
                return description.GetString();
            }

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString();
            }
        }
        catch
        {
            // Ignore parse failures and return null.
        }

        return null;
    }

    private string GetZohoAccountsHost(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            baseUrl = baseUrl.Trim();
            if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }
            Uri.TryCreate(baseUrl, UriKind.Absolute, out uri);
        }

        var host = uri?.Host ?? baseUrl;
        if (host.Contains("zoho.eu", StringComparison.OrdinalIgnoreCase)) return "accounts.zoho.eu";
        if (host.Contains("zoho.in", StringComparison.OrdinalIgnoreCase)) return "accounts.zoho.in";
        if (host.Contains("zoho.com.au", StringComparison.OrdinalIgnoreCase)) return "accounts.zoho.com.au";
        return "accounts.zoho.com";
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
    }
}

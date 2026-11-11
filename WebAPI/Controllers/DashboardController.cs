using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid user token." });
        }

        var user = await _dbContext.Users
            .Include(u => u.Subscriptions)
                .ThenInclude(s => s.Plan)
            .Include(u => u.ZohoConfiguration)
            .SingleOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var now = DateTime.UtcNow;
        var subscription = user.Subscriptions
            .Where(s => s.IsActive && s.ExpiryDate >= now)
            .OrderByDescending(s => s.ExpiryDate)
            .FirstOrDefault();

        var summary = new
        {
            user = new
            {
                user.Id,
                user.Email,
                user.FullName,
                role = string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role,
                lastLogin = user.LastLoginUtc?.ToString("yyyy-MM-dd HH:mm") ?? "Never"
            },
            activePlan = subscription?.Plan?.Name ?? "Free",
            subscriptionStatus = subscription is not null ? "Active" : "Inactive",
            expiryDate = subscription?.ExpiryDate.ToString("yyyy-MM-dd") ?? "N/A",
            remainingDays = subscription is not null ? (subscription.ExpiryDate - now).Days : 0,
            zohoConfig = new
            {
                baseUrl = user.ZohoConfiguration is not null && !string.IsNullOrWhiteSpace(user.ZohoConfiguration.BaseUrl)
                    ? user.ZohoConfiguration.BaseUrl
                    : "https://www.zohoapis.com/books/v3",
                clientId = user.ZohoConfiguration is not null ? user.ZohoConfiguration.ClientId : "Not configured",
                organizationId = user.ZohoConfiguration?.OrganizationId ?? "Not configured",
                status = user.ZohoConfiguration is not null ? "Validated" : "Not configured",
                lastValidated = user.ZohoConfiguration?.LastSyncAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"
            }
        };

        return Ok(summary);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PluginController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public PluginController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] PluginValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new PluginValidateResponse
            {
                IsValid = false,
                Message = "Email is required."
            });
        }

        // Find the user
        var user = await _dbContext.Users
            .Include(u => u.Subscriptions)
            .ThenInclude(s => s.Plan)
            .Include(u => u.ZohoConfiguration)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null)
        {
            return Ok(new PluginValidateResponse
            {
                IsValid = false,
                Message = "User not found."
            });
        }

        // Check if user is active
        if (!user.IsActive)
        {
            return Ok(new PluginValidateResponse
            {
                IsValid = false,
                Message = "User account is disabled."
            });
        }

        // Check for active subscription
        var now = DateTime.UtcNow;
        var activeSubscription = user.Subscriptions?
            .Where(s => s.IsActive && s.ExpiryDate >= now)
            .OrderByDescending(s => s.ExpiryDate)
            .FirstOrDefault();

        if (activeSubscription is null)
        {
            return Ok(new PluginValidateResponse
            {
                IsValid = false,
                Message = "No active subscription found.",
                HasZohoConfig = user.ZohoConfiguration != null
            });
        }

        // Check for Zoho configuration
        if (user.ZohoConfiguration is null)
        {
            return Ok(new PluginValidateResponse
            {
                IsValid = false,
                Message = "Zoho configuration not set up.",
                PlanName = activeSubscription.Plan?.Name,
                ExpiryDate = activeSubscription.ExpiryDate,
                HasZohoConfig = false
            });
        }

        // All validations passed
        return Ok(new PluginValidateResponse
        {
            IsValid = true,
            PlanName = activeSubscription.Plan?.Name,
            ExpiryDate = activeSubscription.ExpiryDate,
            HasZohoConfig = true,
            Message = "Plugin access granted."
        });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseRequest request)
    {
        if (request.PlanId <= 0) return BadRequest(new { error = "Invalid plan id." });

        var plan = await _dbContext.Plans.FindAsync(request.PlanId);
        if (plan is null || !plan.IsActive) return NotFound(new { error = "Plan not found." });

        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid user token." });

        var start = DateTime.UtcNow;
        var expiry = start.AddDays(plan.DurationDays);

        var subscription = new Subscription
        {
            UserId = userId,
            PlanId = plan.Id,
            StartDate = start,
            ExpiryDate = expiry,
            IsActive = true
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Subscription created.", subscription.Id, expiryDate = expiry.ToString("yyyy-MM-dd") });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid user token." });

        var now = DateTime.UtcNow;
        var subscription = await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.IsActive && s.ExpiryDate >= now)
            .OrderByDescending(s => s.ExpiryDate)
            .FirstOrDefaultAsync();

        if (subscription is null)
            return Ok(new { active = false });

        return Ok(new
        {
            active = true,
            plan = subscription.Plan?.Name,
            expiryDate = subscription.ExpiryDate.ToString("yyyy-MM-dd")
        });
    }
}

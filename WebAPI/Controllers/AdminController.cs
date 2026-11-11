using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public AdminController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(string? search = null, int page = 1, int pageSize = 10)
    {
        var q = _dbContext.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(u => u.Email.Contains(search) || u.FullName.Contains(search));
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await q.CountAsync();
        var users = await q
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.Email, u.FullName, u.IsActive })
            .ToListAsync();

        return Ok(new
        {
            items = users,
            totalCount,
            page,
            pageSize
        });
    }

    [HttpPost("users/{id}/disable")]
    public async Task<IActionResult> DisableUser(int id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user is null) return NotFound(new { error = "User not found." });
        user.IsActive = false;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "User disabled." });
    }

    [HttpPost("users/{id}/enable")]
    public async Task<IActionResult> EnableUser(int id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user is null) return NotFound(new { error = "User not found." });
        user.IsActive = true;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "User enabled." });
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions()
    {
        var list = await _dbContext.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.User)
            .Select(s => new {
                s.Id,
                userId = s.UserId,
                userEmail = s.User != null ? s.User.Email : string.Empty,
                plan = s.Plan != null ? s.Plan.Name : string.Empty,
                s.StartDate,
                s.ExpiryDate,
                s.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _dbContext.Users.CountAsync();
        var activeUsers = await _dbContext.Users.CountAsync(u => u.IsActive);
        var inactiveUsers = totalUsers - activeUsers;
        var totalSubscriptions = await _dbContext.Subscriptions.CountAsync();
        var activeSubscriptions = await _dbContext.Subscriptions.CountAsync(s => s.IsActive && s.ExpiryDate >= DateTime.UtcNow);
        var expiredSubscriptions = await _dbContext.Subscriptions.CountAsync(s => !s.IsActive || s.ExpiryDate < DateTime.UtcNow);

        return Ok(new
        {
            totalUsers,
            activeUsers,
            inactiveUsers,
            totalSubscriptions,
            activeSubscriptions,
            expiredSubscriptions
        });
    }

    [HttpPost("users/{id}/subscriptions/assign")]
    public async Task<IActionResult> AssignSubscription(int id, [FromBody] PurchaseRequest req)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user is null) return NotFound(new { error = "User not found." });

        var plan = await _dbContext.Plans.FindAsync(req.PlanId);
        if (plan is null) return NotFound(new { error = "Plan not found." });

        var start = DateTime.UtcNow;
        var expiry = start.AddDays(plan.DurationDays);

        var sub = new Subscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            StartDate = start,
            ExpiryDate = expiry,
            IsActive = true
        };

        _dbContext.Subscriptions.Add(sub);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Subscription assigned.", subscriptionId = sub.Id, expiryDate = sub.ExpiryDate });
    }

    [HttpPost("users/{id}/subscriptions/{subId}/extend")]
    public async Task<IActionResult> ExtendSubscription(int id, int subId, [FromBody] ExtendRequest req)
    {
        var sub = await _dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subId && s.UserId == id);
        if (sub is null) return NotFound(new { error = "Subscription not found." });
        if (!sub.IsActive) return BadRequest(new { error = "Subscription is not active." });

        sub.ExpiryDate = sub.ExpiryDate.AddDays(req.ExtraDays);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Subscription extended.", newExpiry = sub.ExpiryDate });
    }

    [HttpPost("users/{id}/subscriptions/{subId}/cancel")]
    public async Task<IActionResult> CancelSubscription(int id, int subId)
    {
        var sub = await _dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subId && s.UserId == id);
        if (sub is null) return NotFound(new { error = "Subscription not found." });

        sub.IsActive = false;
        sub.ExpiryDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Subscription cancelled." });
    }
}

using Microsoft.AspNetCore.Mvc;
using ZohoPowerBIConnector.WebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public PlansController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _dbContext.Plans
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Description, p.Price, p.DurationDays })
            .ToListAsync();

        return Ok(plans);
    }
}

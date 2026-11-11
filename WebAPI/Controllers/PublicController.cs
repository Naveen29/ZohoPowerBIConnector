using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public PublicController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetPublicStats()
    {
        var totalUsers = await _dbContext.Users.CountAsync();
        return Ok(new { totalUsers });
    }
}

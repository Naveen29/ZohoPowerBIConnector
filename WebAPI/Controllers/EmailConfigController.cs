using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class EmailConfigController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public EmailConfigController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var config = await _dbContext.EmailConfigs.FirstOrDefaultAsync(ec => ec.IsActive);
        if (config is null)
        {
            return Ok(new { exists = false });
        }

        return Ok(new
        {
            exists = true,
            config.Id,
            config.SmtpHost,
            config.SmtpPort,
            config.Username,
            config.EnableSSL,
            config.FromEmail,
            config.FromName,
            config.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmailConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SmtpHost) || request.SmtpPort <= 0 || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FromEmail))
        {
            return BadRequest(new { error = "SMTP host, port, username, password, and from email are required." });
        }

        var existing = await _dbContext.EmailConfigs.FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.SmtpHost = request.SmtpHost.Trim();
            existing.SmtpPort = request.SmtpPort;
            existing.Username = request.Username.Trim();
            existing.Password = request.Password;
            existing.EnableSSL = request.EnableSSL;
            existing.FromEmail = request.FromEmail.Trim();
            existing.FromName = request.FromName.Trim();
            existing.IsActive = true;
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Email configuration updated." });
        }

        var config = new EmailConfig
        {
            SmtpHost = request.SmtpHost.Trim(),
            SmtpPort = request.SmtpPort,
            Username = request.Username.Trim(),
            Password = request.Password,
            EnableSSL = request.EnableSSL,
            FromEmail = request.FromEmail.Trim(),
            FromName = request.FromName.Trim(),
            IsActive = true
        };

        _dbContext.EmailConfigs.Add(config);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = config.Id }, new { message = "Email configuration saved." });
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable()
    {
        var config = await _dbContext.EmailConfigs.FirstOrDefaultAsync(ec => ec.IsActive);
        if (config is null) return NotFound(new { error = "Active email configuration not found." });
        config.IsActive = false;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Email configuration disabled." });
    }
}

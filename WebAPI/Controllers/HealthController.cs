using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public HealthController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        bool dbConnected;
        string? error = null;
        string? failedAt = null;

        try
        {
            var conn = _dbContext.Database.GetDbConnection();
            failedAt = "CanConnectAsync";
            dbConnected = await _dbContext.Database.CanConnectAsync();

            if (!dbConnected)
            {
                failedAt = "SqlConnection.OpenAsync";
                await using var sqlConnection = new SqlConnection(conn.ConnectionString);
                await sqlConnection.OpenAsync();
                dbConnected = sqlConnection.State == System.Data.ConnectionState.Open;
            }
        }
        catch (Exception ex)
        {
            dbConnected = false;
            error = ex.Message;
        }

        if (!dbConnected)
        {
            return StatusCode(503, new
            {
                status = "Unhealthy",
                dbConnected = false,
                failedAt,
                error,
                service = "ZohoPowerBIConnector WebAPI",
                timestamp = DateTime.UtcNow
            });
        }

        return Ok(new
        {
            status = "Healthy",
            dbConnected = true,
            service = "ZohoPowerBIConnector WebAPI",
            timestamp = DateTime.UtcNow
        });
    }
}

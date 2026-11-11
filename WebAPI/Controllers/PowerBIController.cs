using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;


namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PowerBIController : ControllerBase
{
    private readonly ILogger<PowerBIController> _logger;
    private readonly List<ZohoModule> _modules;
    private readonly Models.ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public PowerBIController(ILogger<PowerBIController> logger, List<ZohoModule> modules, Models.ApplicationDbContext dbContext, IConfiguration configuration)
    {
        _logger = logger;
        _modules = modules ?? new List<ZohoModule>();
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>
    /// Returns the schema of enabled Zoho modules and their public endpoints.
    /// </summary>
    /// <remarks>
    /// This metadata endpoint is used by Power BI clients to discover which Zoho module endpoints are available for reporting.
    /// </remarks>
    [HttpGet("schema")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Schema()
    {
        try
        {
            var enabled = _modules.Where(m => m.IsEnabled())
                                  .Select(m => new { name = m.Name, endpoint = $"/api/powerbi/{m.Endpoint}" })
                                  .ToList();

            return Ok(new { tables = enabled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Power BI schema");
            return StatusCode(500, new { error = "Unable to build schema" });
        }
    }

    // Additional dynamic endpoints will be implemented later.
    /// <summary>
    /// Generic data endpoint for enabled Zoho modules. Supports pagination, filtering, and sorting.
    /// </summary>
    /// <param name="endpoint">The module endpoint to fetch, for example <c>items</c> or <c>invoices</c>.</param>
    /// <param name="page">The page number for paginated results.</param>
    /// <param name="pageSize">The number of rows to return per page.</param>
    /// <param name="from">Filter records modified on or after this date.</param>
    /// <param name="to">Filter records modified on or before this date.</param>
    /// <param name="modifiedAfter">Filter records modified after this date/time.</param>
    /// <param name="sort">The column to sort by. Allowed values: <c>ModifiedAt</c>, <c>Id</c>, <c>CreatedAt</c>, <c>Name</c>.</param>
    /// <param name="order">Sort direction: <c>asc</c> or <c>desc</c>.</param>
    /// <remarks>
    /// This endpoint enforces tenant isolation by filtering results to the authenticated user's Zoho client.
    /// </remarks>
    [HttpGet("{endpoint}")]
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModuleData(string endpoint, [FromQuery] int page = 1, [FromQuery] int pageSize = 5000,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? modifiedAfter = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null)
    {
        try
        {
            var module = _modules.FirstOrDefault(m => string.Equals(m.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase) && m.IsEnabled());
            if (module is null)
            {
                return NotFound(new { error = "Module not found or not enabled." });
            }

            // resolve table name from StoredProcedure if present, fallback to Name
            var tableSuffix = "";
            if (!string.IsNullOrWhiteSpace(module.StoredProcedure) && module.StoredProcedure.StartsWith("usp_Merge", StringComparison.OrdinalIgnoreCase))
            {
                tableSuffix = module.StoredProcedure.Substring("usp_Merge".Length);
            }
            else if (!string.IsNullOrWhiteSpace(module.Name))
            {
                tableSuffix = new string(module.Name.Where(char.IsLetterOrDigit).ToArray());
            }

            if (string.IsNullOrWhiteSpace(tableSuffix)) return BadRequest(new { error = "Unable to determine target table for module." });

            var tableName = "Zoho" + tableSuffix;

            // verify tableName is in allowed modules (prevents SQL injection)
            var allowed = _modules.Select(m =>
            {
                var sfx = !string.IsNullOrWhiteSpace(m.StoredProcedure) && m.StoredProcedure.StartsWith("usp_Merge", StringComparison.OrdinalIgnoreCase)
                    ? m.StoredProcedure.Substring("usp_Merge".Length)
                    : (m.Name != null ? new string(m.Name.Where(char.IsLetterOrDigit).ToArray()) : string.Empty);
                return "Zoho" + sfx;
            }).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowed.Contains(tableName)) return BadRequest(new { error = "Module table is not available." });

            // get current user id from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { error = "Unable to determine user identity." });
            }

            // verify user account is active
            var user = await _dbContext.Users.FindAsync(userId);
            if (user is null) return Unauthorized(new { error = "User not found." });
            if (!user.IsActive) return StatusCode(403, new { error = "User account is not active." });

            // verify active subscription exists
            var hasActiveSubscription = await _dbContext.Subscriptions.AnyAsync(s => s.UserId == userId && s.IsActive && s.ExpiryDate >= DateTime.UtcNow);
            if (!hasActiveSubscription) return StatusCode(403, new { error = "An active subscription is required to access this data." });

            

            var clientId = userId; // Assuming UserId is the Zoho client ID. Adjust if necessary.;

            // build SQL query
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 10000);

            var where = "WHERE ClientId = @clientId";
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@clientId", clientId)
            };

            if (!string.IsNullOrWhiteSpace(modifiedAfter) && DateTime.TryParse(modifiedAfter, out var mAfter))
            {
                where += " AND LastModifiedTime > @modifiedAfter";
                parameters.Add(new SqlParameter("@modifiedAfter", mAfter));
            }
            if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var fDate))
            {
                where += " AND LastModifiedTime >= @fromDate";
                parameters.Add(new SqlParameter("@fromDate", fDate));
            }
            if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var tDate))
            {
                where += " AND LastModifiedTime <= @toDate";
                parameters.Add(new SqlParameter("@toDate", tDate));
            }

            // Only allow a small whitelist of sortable columns to prevent SQL injection.
            // These map client-friendly sort keys to actual database columns.
            var sortMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ModifiedAt", "LastModifiedTime" },
                { "CreatedAt", "CreatedTime" },
                { "Id", "InvoiceId" },
                { "Name", "CustomerName" }
            };

            var effectiveSort = string.IsNullOrWhiteSpace(sort) ? "ModifiedAt" : sort;
            if (!sortMappings.TryGetValue(effectiveSort, out var sortCol))
            {
                sortCol = "LastModifiedTime";
            }

            var sortOrder = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

            var stopwatch = Stopwatch.StartNew();
            var offset = (page - 1) * pageSize;
            var sql = $"SELECT * FROM [{tableName}] {where} ORDER BY [{sortCol}] {sortOrder} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

            _logger.LogInformation("Power BI request for module {Endpoint} from user {UserId}. Page={Page}, PageSize={PageSize}, Sort={Sort}, Order={Order}", endpoint, userId, page, pageSize, sortCol, sortOrder);

            var conn = _dbContext.Database.GetDbConnection();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;
                foreach (var p in parameters) cmd.Parameters.Add(p);

                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                var list = new List<Dictionary<string, object?>>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        var val = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                        row[name] = val;
                    }
                    list.Add(row);
                }

                stopwatch.Stop();
                _logger.LogInformation("Power BI module {Endpoint} returned {RowCount} rows in {ElapsedMilliseconds}ms", endpoint, list.Count, stopwatch.ElapsedMilliseconds);

                return Ok(list);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching module data: {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Returns paginated item records for the authenticated user.
    /// </summary>
    [HttpGet("items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetItems([FromQuery] int page = 1, [FromQuery] int pageSize = 5000,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? modifiedAfter = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null)
        => GetNamedModuleData("Items", page, pageSize, from, to, modifiedAfter, sort, order);

    /// <summary>
    /// Returns paginated customer records for the authenticated user.
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetCustomers([FromQuery] int page = 1, [FromQuery] int pageSize = 5000,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? modifiedAfter = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null)
        => GetNamedModuleData("Customers", page, pageSize, from, to, modifiedAfter, sort, order);

    /// <summary>
    /// Returns paginated invoice records for the authenticated user.
    /// </summary>
    [HttpGet("invoices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetInvoices([FromQuery] int page = 1, [FromQuery] int pageSize = 5000,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? modifiedAfter = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null)
        => GetNamedModuleData("Invoices", page, pageSize, from, to, modifiedAfter, sort, order);

    /// <summary>
    /// Returns paginated payment records for the authenticated user.
    /// </summary>
    [HttpGet("paymentsreceived")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetPaymentsReceived([FromQuery] int page = 1, [FromQuery] int pageSize = 5000,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? modifiedAfter = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null)
        => GetNamedModuleData("PaymentsReceived", page, pageSize, from, to, modifiedAfter, sort, order);

    private Task<IActionResult> GetNamedModuleData(string moduleName, int page, int pageSize, string? from, string? to, string? modifiedAfter, string? sort, string? order)
    {
        var module = _modules.FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase) && m.IsEnabled());
        if (module is null)
        {
            return Task.FromResult<IActionResult>(NotFound(new { error = $"Module '{moduleName}' is not enabled or does not exist." }));
        }

        return GetModuleData(module.Endpoint ?? moduleName.ToLowerInvariant(), page, pageSize, from, to, modifiedAfter, sort, order);
    }
}

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace ZohoPowerBIConnector.WebAPI.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // limit to 120 requests per 60 seconds per IP by default
    private const int LIMIT = 120;
    private static readonly TimeSpan WINDOW = TimeSpan.FromSeconds(60);

    public RateLimitMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var key = $"rl_{ip}";
            var counter = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WINDOW;
                return new RateCounter { Timestamp = DateTime.UtcNow, Count = 0 };
            });

            lock (counter)
            {
                counter.Count++;
                if (counter.Count > LIMIT)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = ((int)WINDOW.TotalSeconds).ToString();
                    _logger.LogWarning("Rate limit exceeded for {IP}", ip);
                    return;
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limit middleware");
            await _next(context);
        }
    }

    private class RateCounter
    {
        public DateTime Timestamp { get; set; }
        public int Count { get; set; }
    }
}

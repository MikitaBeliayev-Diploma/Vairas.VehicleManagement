using System.Diagnostics;

namespace Vairas.VehicleManagement.Middleware;

/// <summary>Logs method, route template, status and duration only; never paths with identifiers or query strings.</summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var start = Stopwatch.GetTimestamp();
        await next(http);
        logger.LogInformation("HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs} ms",
            http.Request.Method,
            (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched",
            http.Response.StatusCode,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}

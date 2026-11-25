using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Middleware;
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public static string Id(HttpContext http) => (string? )http.Items["CorrelationId"] ?? http.TraceIdentifier;
    public async Task InvokeAsync(HttpContext http, ILogger<CorrelationMiddleware> logger)
    {
        var supplied = http.Request.Headers["X-Correlation-ID"].ToString();
        var id = supplied.Length is> 0 and <= 128 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? supplied : Guid.NewGuid().ToString("D");
        http.Items["CorrelationId"] = id;
        http.Response.Headers["X-Correlation-ID"] = id;
        http.Response.Headers.CacheControl = "no-store";
        http.Response.OnStarting(() =>
        {
            http.Response.Headers["X-Correlation-ID"] = id;
            http.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next(http);
    }
}

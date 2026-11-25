using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Middleware;
public static class Problems
{
    public static async Task WriteProblemAsync(this HttpContext http, int status, string code, string title, CancellationToken ct = default)
    {
        http.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = http.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationMiddleware.Id(http);
        await http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
    }
}

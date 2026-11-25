using Microsoft.AspNetCore.Mvc;
using Vairas.VehicleManagement.Middleware;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureExceptionHandling
{
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ProblemDetails { Status = 400, Title = "The request is invalid.", Instance = context.HttpContext.Request.Path };
            problem.Extensions["code"] = "VALIDATION_FAILED";
            problem.Extensions["correlationId"] = CorrelationMiddleware.Id(context.HttpContext);
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        });
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }
}

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureOpenApi
{
    public static IServiceCollection AddOpenApiDocument(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentTransformer>());

    /// <summary>The OpenAPI document is served only by local Development and Testing hosts.</summary>
    public static WebApplication MapOpenApiDocument(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            app.MapOpenApi();
        return app;
    }
}

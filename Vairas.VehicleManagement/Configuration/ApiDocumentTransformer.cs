using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace Vairas.VehicleManagement.Configuration;
public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "Vairas Vehicle Management API";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Tenant-authorized fleet resources and durable planned state changes. Maintenance is reserved. Scheduling gate is an external dependency; disabled coordination returns 503 before effects.";
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["bearerAuth"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" },
            ["workloadBearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "vehicles.eligibility.read scope and configured workload allowlist." }
        };
        foreach (var (path, item) in document.Paths)
            foreach (var operation in item.Operations!.Values)
            {
                operation.Security = [new() { [new OpenApiSecuritySchemeReference(path.StartsWith("/internal/", StringComparison.Ordinal) ? "workloadBearer" : "bearerAuth", document)] = [] }];
                foreach (var parameter in operation.Parameters ?? [])
                    if (parameter is OpenApiParameter p)
                    {
                        if (p.Name == "Idempotency-Key") { p.Required = true; p.Description = "8..128 printable ASCII characters, scoped by actor, school and command; changed payload conflicts."; }
                        if (p.Name is "schoolId" or "branchId" or "teacherId" or "category") p.Required = true;
                    }
                operation.Responses ??= new();
                foreach (var status in new[] { "400", "401", "403", "404", "409", "415", "422", "500", "503" })
                    operation.Responses.TryAdd(status, new OpenApiResponse { Description = "ProblemDetails with stable code and correlationId; credentials and foreign resource details are concealed.", Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } } } });
                if (operation.Responses.TryGetValue("201", out var createdResponse) && createdResponse is OpenApiResponse created)
                    created.Headers = new Dictionary<string, IOpenApiHeader> { ["Location"] = new OpenApiHeader { Description = "Created resource URL", Schema = new OpenApiSchema { Type = JsonSchemaType.String } } };
                if (operation.Responses.TryGetValue("202", out var acceptedResponse) && acceptedResponse is OpenApiResponse accepted)
                    accepted.Headers = new Dictionary<string, IOpenApiHeader>
                    {
                        ["Location"] = new OpenApiHeader { Description = "Authoritative operation status URL", Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                        ["Retry-After"] = new OpenApiHeader { Description = "Polling interval in seconds", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } }
                    };
            }
        return Task.CompletedTask;
    }
}

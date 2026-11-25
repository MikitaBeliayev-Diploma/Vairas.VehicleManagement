using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Configuration;
using Vairas.VehicleManagement.Middleware;
using Vairas.VehicleManagement.Startup;

if (args.Contains("--provision")) { await DatabaseProvisioning.ProvisionAsync(); return; }
if (args.Contains("--migrate")) { await DatabaseMigration.MigrateAsync(); return; }

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();
builder.Services
    .AddVehicleOptions(builder.Configuration)
    .AddStrictJsonSerialization()
    .AddExceptionHandling()
    .AddOpenApiDocument()
    .AddVehicleDatabase()
    .AddVehicleServices()
    .AddDependencyHttpClients()
    .AddMessageBroker()
    .AddBearerAuthentication()
    .AddEntryAuthorization()
    .AddServiceHealthChecks()
    .AddServiceTelemetry(builder.Configuration);

var app = builder.Build();

// Fail fast: the category catalogue is loaded and verified before the first request.
_ = app.Services.GetRequiredService<ICategoryCatalogue>();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(status => status.HttpContext.WriteProblemAsync(status.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed."));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapServiceHealthChecks();
app.MapOpenApiDocument();
app.Run();

public partial class Program;

using OpenTelemetry;
using Vairas.VehicleManagement.Business;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Vairas.VehicleManagement.Configuration;

public static class ConfigureTelemetry
{
    public static IServiceCollection AddServiceTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(VehicleTelemetry.Name))
            .WithTracing(tracing => tracing.AddSource(VehicleTelemetry.Name).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics.AddMeter(VehicleTelemetry.Name).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            telemetry.UseOtlpExporter();
        return services;
    }
}

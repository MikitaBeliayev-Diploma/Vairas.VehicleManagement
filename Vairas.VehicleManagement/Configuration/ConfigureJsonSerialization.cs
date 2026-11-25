using System.Text.Json.Serialization;

namespace Vairas.VehicleManagement.Configuration;

/// <summary>Strict JSON contract shared by minimal-API and MVC serialization: no integer enums, no unknown members.</summary>
public static class ConfigureJsonSerialization
{
    public static IServiceCollection AddStrictJsonSerialization(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        services.AddControllers().AddJsonOptions(options =>
        {
            var json = options.JsonSerializerOptions;
            json.PropertyNameCaseInsensitive = false;
            json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            json.AllowDuplicateProperties = false;
            json.RespectRequiredConstructorParameters = true;
            json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        });
        return services;
    }
}

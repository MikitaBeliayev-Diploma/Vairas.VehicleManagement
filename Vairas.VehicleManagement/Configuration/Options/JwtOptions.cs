namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class JwtOptions
{
    public string Authority { get; set; } = "https://localhost:7100";
    public string Issuer { get; set; } = "https://localhost:7100";
    public string UserAudience { get; set; } = "vairas-vehicle-management";
    public string WorkloadAudience { get; set; } = "vairas-vehicle-management-internal";
    // Local allowlist intersects with signed scopes; absent configuration grants nothing.
    public Dictionary<string, string[]> WorkloadClients { get; set; } = [];
}

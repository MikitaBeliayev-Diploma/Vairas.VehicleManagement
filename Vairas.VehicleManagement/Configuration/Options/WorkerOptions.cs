namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class WorkerOptions
{
    public bool Enabled { get; set; } = true;
    public bool OutboxEnabled { get; set; }
    public string ServiceBusNamespace { get; set; } = "";
    /// <summary>Local/emulator only. When set, it is used instead of the namespace + managed identity.</summary>
    public string ConnectionString { get; set; } = "";
    public string Topic { get; set; } = "vairas-vehicle";
    public string? ManagedIdentityClientId { get; set; }
}

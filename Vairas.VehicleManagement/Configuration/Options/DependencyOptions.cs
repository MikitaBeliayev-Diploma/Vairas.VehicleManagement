namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class DependencyOptions
{
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string UserManagementBaseUrl { get; set; } = "https://localhost:7001/";
    public string SchoolBaseUrl { get; set; } = "https://localhost:7200/";
    public string SchedulingBaseUrl { get; set; } = "https://localhost:7300/";
    public string ClientId { get; set; } = "vehicle-management";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "identity.validate profiles.status.read profiles.binding.read school.vehicle-authority.read scheduling.vehicle-gate";
}

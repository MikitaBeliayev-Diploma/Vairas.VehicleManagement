namespace Vairas.VehicleManagement.Domain;
// Reserved canonical aggregate. No Maintenance write surface is enabled in v1.
public sealed class Maintenance
{
    public Guid MaintenanceId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid VehicleId { get; set; }
    public MaintenanceType Type { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public MaintenanceStatus Status { get; set; }
    public string? Comment { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

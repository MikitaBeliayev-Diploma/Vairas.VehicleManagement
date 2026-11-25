namespace Vairas.VehicleManagement.Domain;
public sealed class VehicleOperation
{
    public Guid OperationId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? MaintenanceId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorService { get; set; }
    public Channel Channel { get; set; }
    public string CommandName { get; set; } = "";
    public string RequestFingerprint { get; set; } = "";
    public long? ExpectedVehicleRevision { get; set; }
    public long? ExpectedMaintenanceRevision { get; set; }
    public long ResultingVehicleRevision { get; set; }
    public long? ResultingMaintenanceRevision { get; set; }
    public string? Reason { get; set; }
    public Guid? ConfirmationId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

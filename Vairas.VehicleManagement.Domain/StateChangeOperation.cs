namespace Vairas.VehicleManagement.Domain;
public sealed class StateChangeOperation
{
    public Guid OperationId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid BranchId { get; set; }
    public StateChangeType OperationType { get; set; }
    public SagaStatus Status { get; set; }
    public SagaStatus ResumeStatus { get; set; }
    public long ExpectedVehicleRevision { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Reason { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public Guid? BlockId { get; set; }
    public Guid? BlockOperationId { get; set; }
    public Guid ReleaseOperationId { get; set; }
    public VehicleStatus? ResultVehicleStatus { get; set; }
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

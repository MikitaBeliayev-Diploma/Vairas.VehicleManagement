namespace Vairas.VehicleManagement.Domain;

public enum SagaStatus
{
    Pending,
    Blocking,
    SchedulingBlocked,
    ApplyingVehicleState,
    ReleasingBlock,
    Completed,
    Rejected,
    NeedsRecovery
}

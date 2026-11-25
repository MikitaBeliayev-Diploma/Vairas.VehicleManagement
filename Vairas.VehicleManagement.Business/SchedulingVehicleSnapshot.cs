namespace Vairas.VehicleManagement.Business;

/// <summary>Exact short-lived source snapshot; reservation decisions remain in Scheduling.</summary>
public sealed record SchedulingVehicleSnapshot(Guid VehicleId,Guid SchoolId,Guid TeacherId,Guid BranchId,string Category,long Revision,bool Eligible,bool GateSupported,DateTimeOffset ValidUntil);

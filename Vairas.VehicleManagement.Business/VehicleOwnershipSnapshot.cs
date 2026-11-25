namespace Vairas.VehicleManagement.Business;

/// <summary>Minimal verified ownership for the Scheduling vehicle gate.</summary>
public sealed record VehicleOwnershipSnapshot(Guid VehicleId,Guid SchoolId,long Revision,bool GateSupported,DateTimeOffset ValidUntil);

using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Request a durable planned status change with optimistic concurrency.</summary>
public sealed record StateChangeRequest(StateChangeType OperationType, long ExpectedVehicleRevision, string Reason);

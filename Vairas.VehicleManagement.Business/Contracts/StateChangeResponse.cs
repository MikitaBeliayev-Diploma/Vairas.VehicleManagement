using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Persisted status of one state-change Saga; 202 means processing, not completion.</summary>
public sealed record StateChangeResponse(Guid OperationId, Guid VehicleId, StateChangeType OperationType, SagaStatus Status, Guid? BlockId, VehicleStatus? ResultVehicleStatus, string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

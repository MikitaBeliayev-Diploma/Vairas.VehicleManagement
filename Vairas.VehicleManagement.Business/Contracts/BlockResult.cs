using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public sealed record BlockResult(Guid OperationId, Guid VehicleId, string Status, Guid? BlockId, string? ErrorCode);

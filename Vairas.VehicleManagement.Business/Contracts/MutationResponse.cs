using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Exact saved command result, including whether this was an authorized retry.</summary>
public sealed record MutationResponse(Guid OperationId, VehicleResponse Resource, bool Replayed);

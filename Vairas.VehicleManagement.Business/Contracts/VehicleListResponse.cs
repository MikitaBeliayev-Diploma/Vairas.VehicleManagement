using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Bounded vehicle page with an exclusive UUID cursor.</summary>
public sealed record VehicleListResponse(IReadOnlyList<VehicleResponse> Items, Guid? NextCursor);

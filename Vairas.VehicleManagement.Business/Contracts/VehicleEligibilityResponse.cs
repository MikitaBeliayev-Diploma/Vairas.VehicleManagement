using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Current operational preflight; never a reservation or a reusable admission permit.</summary>
public sealed record VehicleEligibilityResponse(Guid VehicleId, bool Eligible, VehicleStatus VehicleStatus, long VehicleVersion, string[] Reasons);

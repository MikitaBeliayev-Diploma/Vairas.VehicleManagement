using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Read-only exact school/branch/actor/teacher authority query.</summary>
public sealed record VehicleAuthorityRequest(Guid SchoolId, Guid? BranchId = null, Guid? ActorUserId = null, Guid? TeacherId = null, Guid? SchoolMembershipId = null);

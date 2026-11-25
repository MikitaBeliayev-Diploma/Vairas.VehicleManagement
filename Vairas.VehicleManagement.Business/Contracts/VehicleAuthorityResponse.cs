using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Current school-owned authority basis; identifiers echo the validated request.</summary>
public sealed record VehicleAuthorityResponse(bool Allowed, Guid SchoolId, Guid? BranchId, Guid? ActorUserId, Guid? TeacherId, Guid? SchoolMembershipId, Guid? TeacherUserId, long SchoolRevision, long? BranchRevision, long? MembershipRevision, DateTimeOffset ValidUntil);

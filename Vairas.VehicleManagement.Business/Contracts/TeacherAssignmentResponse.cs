using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>One current vehicle teacher assignment and its exact authority basis.</summary>
public sealed record TeacherAssignmentResponse(Guid TeacherId, Guid SchoolMembershipId, DateTimeOffset AssignedAt);

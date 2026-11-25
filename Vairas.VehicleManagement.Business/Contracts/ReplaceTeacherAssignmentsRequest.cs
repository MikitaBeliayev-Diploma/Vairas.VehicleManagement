using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Replace teacher assignments; the server resolves exact current memberships.</summary>
public sealed record ReplaceTeacherAssignmentsRequest(long ExpectedRevision, Guid[] TeacherIds);

using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class TestUsers : IUserProfiles
{
    public HashSet<Guid> Inactive { get; } = [];
    public bool BrokenBinding { get; set; }
    public Task RequireActiveAsync(Guid userId, CancellationToken ct)
    { if (Inactive.Contains(userId)) throw VehicleErrors.Denied(); return Task.CompletedTask; }
    public Task<ProfileBinding> TeacherAsync(Guid teacherId, CancellationToken ct) => Task.FromResult(new ProfileBinding("Teacher", teacherId, BrokenBinding ? Guid.NewGuid() : teacherId, !Inactive.Contains(teacherId)));
}

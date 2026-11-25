using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class TestAuthority(FixedClock clock) : ISchoolAuthority
{
    public bool Allowed { get; set; } = true;
    public bool Expired { get; set; }
    public Dictionary<Guid, Guid> Memberships { get; } = [];
    public Func<VehicleAuthorityRequest, VehicleAuthorityResponse, VehicleAuthorityResponse>? Alter { get; set; }
    public Task<VehicleAuthorityResponse> ValidateAsync(VehicleAuthorityRequest request, CancellationToken ct)
    {
        Guid? membership = request.TeacherId is Guid teacher ? Memberships.GetValueOrDefault(teacher, teacher) : null;
        var result = new VehicleAuthorityResponse(Allowed && (!request.SchoolMembershipId.HasValue || request.SchoolMembershipId == membership), request.SchoolId,
            request.BranchId, request.ActorUserId, request.TeacherId, membership, request.TeacherId, 1, request.BranchId.HasValue ? 1 : null,
            membership.HasValue ? 1 : null, Expired ? clock.Now.AddSeconds(-1) : clock.Now.AddSeconds(30));
        return Task.FromResult(Alter?.Invoke(request, result) ?? result);
    }
}

using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed partial class VehicleService
{
    public async Task<VehicleResponse> GetAsync(Guid school, Guid vehicle, VehicleActor actor, CancellationToken ct)
    {
        VehicleRules.Id(vehicle);
        actor = await Authorize(school, null, actor, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var result = await Dto(s, await Find(s, school, vehicle, ct), ct);
        VehicleRules.Fresh(actor, await s.NowAsync(ct));
        return result;
    }

    public async Task<VehicleListResponse> ListAsync(Guid school, Guid? after, int limit, VehicleActor actor, CancellationToken ct)
    {
        if (limit is < 1 or > 100)
            throw VehicleErrors.Invalid();
        if (after.HasValue)
            VehicleRules.Id(after.Value);
        actor = await Authorize(school, null, actor, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var rows = await s.PageAsync(school, after, limit + 1, ct);
        var page = new List<VehicleResponse>();
        foreach (var v in rows.Take(limit))
            page.Add(await Dto(s, v, ct));
        VehicleRules.Fresh(actor, await s.NowAsync(ct));
        return new(page, rows.Count > limit ? page[^1].VehicleId : null);
    }

    public async Task<VehicleEligibilityResponse> EligibilityAsync(Guid school, Guid vehicle, Guid branch, Guid teacher, string category, CancellationToken ct) => (await EligibilityCore(school,vehicle,branch,teacher,category,ct)).Eligibility;

    private async Task<(VehicleEligibilityResponse Eligibility,DateTimeOffset ValidUntil)> EligibilityCore(Guid school,Guid vehicle,Guid branch,Guid teacher,string category,CancellationToken ct)
    {
        VehicleRules.Id(school);
        VehicleRules.Id(vehicle);
        VehicleRules.Id(branch);
        VehicleRules.Id(teacher);
        category = VehicleRules.Text(category, 16).ToUpperInvariant();
        Vehicle v;
        bool supports;
        TeacherAssignment? assignment;
        bool pending;
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            v = await Find(read, school, vehicle, ct);
            supports = catalogue.Contains(category) && await read.FindAsync<VehicleCategory>(x => x.SchoolId == school && x.VehicleId == vehicle && x.CategoryCode == category, ct)is not null;
            assignment = await read.FindAsync<TeacherAssignment>(x => x.SchoolId == school && x.VehicleId == vehicle && x.TeacherId == teacher, ct);
            pending = await HasPendingStateChangeAsync(read, school, vehicle, ct);
        }

        var query = new VehicleAuthorityRequest(school, v.BranchId, null, assignment is null ? null : teacher, assignment?.SchoolMembershipId);
        var authority = await schoolAuthority.ValidateAsync(query, ct);
        DateTimeOffset until;
        var sourceAllowed = true;
        try
        {
            until = VehicleRules.Authority(query, authority, clock.GetUtcNow());
        }
        catch (ForbiddenException)
        {
            sourceAllowed = false;
            until = authority.ValidUntil;
        }

        if (assignment is not null && sourceAllowed)
        {
            var binding = await users.TeacherAsync(teacher, ct);
            if (binding.ProfileId != teacher || binding.Kind != "Teacher" || binding.UserId != authority.TeacherUserId)
                throw VehicleErrors.Dependency();
            sourceAllowed = binding.Active;
            if (sourceAllowed)
                try
                {
                    await users.RequireActiveAsync(binding.UserId, ct);
                }
                catch (ForbiddenException)
                {
                    sourceAllowed = false;
                }
        }

        await using var final = await store.OpenAsync(school, false, ct);
        var current = await Find(final, school, vehicle, ct);
        if (current.Revision != v.Revision || until <= await final.NowAsync(ct))
            throw VehicleErrors.Dependency();
        pending |= await HasPendingStateChangeAsync(final, school, vehicle, ct);
        var reasons = new List<string>();
        if (v.Status != VehicleStatus.Active || pending || !sourceAllowed)
            reasons.Add("STATUS_NOT_ACTIVE");
        if (!supports)
            reasons.Add("CATEGORY_NOT_SUPPORTED");
        if (assignment is null || !sourceAllowed)
            reasons.Add("TEACHER_NOT_ASSIGNED");
        if (v.BranchId != branch)
            reasons.Add("BRANCH_MISMATCH");
        return (new(vehicle, reasons.Count == 0, v.Status, v.Revision, reasons.ToArray()),until);
    }
}

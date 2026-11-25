using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed partial class VehicleService
{
    // External block verification happens before opening the write transaction. Recheck the
    // vehicle revision and the retained owning Saga inside it; never hold a SQL lock across RPC.
    private async Task<Vehicle> Editable(Guid school, Guid vehicle, VehicleActor actor, CancellationToken ct)
    {
        Vehicle v;
        StateChangeOperation? last;
        await using (var s = await store.OpenAsync(school, false, ct))
        {
            v = await Find(s, school, vehicle, ct);
            await EnsureNoSaga(s, v, ct);
            if (v.Status != VehicleStatus.Inactive)
                throw VehicleErrors.Conflict("VEHICLE_MUST_BE_INACTIVE");
            var history = await s.ListAsync<StateChangeOperation>(x => x.SchoolId == school && x.VehicleId == vehicle && x.Status == SagaStatus.Completed, ct);
            last = history.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.OperationId).FirstOrDefault();
            if (last is null && await s.FindAsync<VehicleOperation>(x => x.SchoolId == school && x.VehicleId == vehicle && x.CommandName == "CreateVehicle", ct)is null)
                throw VehicleErrors.Dependency("VEHICLE_ORIGIN_NOT_VERIFIED");
        }

        if (last is not null)
        {
            if (last.OperationType != StateChangeType.Deactivate || last.BlockId is null)
                throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
            var outcome = await gate.OutcomeAsync(last.OperationId, ct);
            if (outcome?.Status != "Active" || outcome.OperationId != last.OperationId || outcome.VehicleId != vehicle || outcome.BlockId != last.BlockId)
                throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
        }

        VehicleRules.Fresh(actor, clock.GetUtcNow());
        return v;
    }

    public async Task<MutationResponse> CategoriesAsync(Guid school, Guid vehicle, string key, ReplaceCategoriesRequest request, VehicleActor actor, CancellationToken ct)
    {
        key = VehicleRules.Key(key);
        VehicleRules.Id(vehicle);
        VehicleRules.Revision(request.ExpectedRevision);
        request = request with
        {
            Categories = VehicleRules.Categories(request.Categories, catalogue)
        };
        var fingerprint = VehicleRules.Fingerprint("ReplaceCategories", school, vehicle, request);
        actor = await Authorize(school, null, actor, ct);
        // Authorized replay precedes current edit prerequisites.
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            var prior = await Replay(read, school, actor.UserId, key, "ReplaceCategories", fingerprint, ct);
            if (prior is not null)
                return Saved(prior);
        }

        var basis = await Editable(school, vehicle, actor, ct);
        await using var s = await store.OpenAsync(school, true, ct);
        var v = await Find(s, school, vehicle, ct);
        var replay = await Replay(s, school, actor.UserId, key, "ReplaceCategories", fingerprint, ct);
        if (replay is not null)
            return Saved(replay);
        await EnsureNoSaga(s, v, ct);
        VehicleRules.Match(basis.Revision, v.Revision);
        VehicleRules.Match(request.ExpectedRevision, v.Revision);
        var old = await s.ListAsync<VehicleCategory>(x => x.SchoolId == school && x.VehicleId == vehicle, ct);
        foreach (var row in old.Where(x => !request.Categories.Contains(x.CategoryCode, StringComparer.Ordinal)))
            s.Remove(row);
        foreach (var code in request.Categories.Except(old.Select(x => x.CategoryCode), StringComparer.Ordinal))
            s.Add(new VehicleCategory { SchoolId = school, VehicleId = vehicle, CategoryCode = code });
        v.Revision++;
        v.UpdatedAt = await s.NowAsync(ct);
        return await Complete(s, v, actor, key, "ReplaceCategories", fingerprint, request.ExpectedRevision, ct);
    }

    public async Task<MutationResponse> TeachersAsync(Guid school, Guid vehicle, string key, ReplaceTeacherAssignmentsRequest request, VehicleActor actor, CancellationToken ct)
    {
        key = VehicleRules.Key(key);
        VehicleRules.Id(vehicle);
        VehicleRules.Revision(request.ExpectedRevision);
        if (request.TeacherIds is null || request.TeacherIds.Length > 100 || request.TeacherIds.Distinct().Count() != request.TeacherIds.Length)
            throw VehicleErrors.Invalid();
        foreach (var teacher in request.TeacherIds)
            VehicleRules.Id(teacher);
        request = request with
        {
            TeacherIds = request.TeacherIds.Order().ToArray()
        };
        var fingerprint = VehicleRules.Fingerprint("ReplaceTeacherAssignments", school, vehicle, request);
        actor = await Authorize(school, null, actor, ct);
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            var prior = await Replay(read, school, actor.UserId, key, "ReplaceTeacherAssignments", fingerprint, ct);
            if (prior is not null)
                return Saved(prior);
        }

        var basis = await Editable(school, vehicle, actor, ct);
        var memberships = new Dictionary<Guid, Guid>();
        foreach (var teacher in request.TeacherIds)
        {
            var binding = await users.TeacherAsync(teacher, ct);
            if (binding.Kind != "Teacher" || binding.ProfileId != teacher || binding.UserId == Guid.Empty || !binding.Active)
                throw VehicleErrors.Denied();
            await users.RequireActiveAsync(binding.UserId, ct);
            var query = new VehicleAuthorityRequest(school, basis.BranchId, actor.UserId, teacher);
            var response = await schoolAuthority.ValidateAsync(query, ct);
            var until = VehicleRules.Authority(query, response, clock.GetUtcNow());
            if (response.TeacherUserId != binding.UserId)
                throw VehicleErrors.Dependency();
            actor = actor with
            {
                ValidUntil = until < actor.ValidUntil ? until : actor.ValidUntil
            };
            memberships.Add(teacher, response.SchoolMembershipId!.Value);
        }

        await using var s = await store.OpenAsync(school, true, ct);
        var v = await Find(s, school, vehicle, ct);
        var replay = await Replay(s, school, actor.UserId, key, "ReplaceTeacherAssignments", fingerprint, ct);
        if (replay is not null)
            return Saved(replay);
        await EnsureNoSaga(s, v, ct);
        VehicleRules.Match(basis.Revision, v.Revision);
        VehicleRules.Match(request.ExpectedRevision, v.Revision);
        var old = await s.ListAsync<TeacherAssignment>(x => x.SchoolId == school && x.VehicleId == vehicle, ct);
        foreach (var row in old.Where(x => !memberships.ContainsKey(x.TeacherId)))
            s.Remove(row);
        var now = await s.NowAsync(ct);
        foreach (var(teacher, membership)in memberships)
        {
            var row = old.SingleOrDefault(x => x.TeacherId == teacher);
            if (row is null)
                s.Add(new TeacherAssignment { SchoolId = school, VehicleId = vehicle, TeacherId = teacher, SchoolMembershipId = membership, AssignedAt = now });
            else if (row.SchoolMembershipId != membership)
            {
                row.SchoolMembershipId = membership;
                row.AssignedAt = now;
            }
        }

        v.Revision++;
        v.UpdatedAt = now;
        return await Complete(s, v, actor, key, "ReplaceTeacherAssignments", fingerprint, request.ExpectedRevision, ct);
    }
}

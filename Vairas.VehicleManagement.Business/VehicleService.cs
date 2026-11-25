using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed partial class VehicleService(IVehicleStore store, ISchoolAuthority schoolAuthority, IUserProfiles users, ICategoryCatalogue catalogue, ISchedulingGate gate, TimeProvider clock) : IVehicleService
{
    private async Task<VehicleActor> Authorize(Guid school, Guid? branch, VehicleActor actor, CancellationToken ct)
    {
        VehicleRules.Id(school);
        VehicleRules.Fresh(actor, clock.GetUtcNow());
        await users.RequireActiveAsync(actor.UserId, ct);
        var query = new VehicleAuthorityRequest(school, branch, actor.UserId);
        var until = VehicleRules.Authority(query, await schoolAuthority.ValidateAsync(query, ct), clock.GetUtcNow());
        return actor with
        {
            ValidUntil = until < actor.ValidUntil ? until : actor.ValidUntil
        };
    }

    private static async Task<Vehicle> Find(IVehicleSession s, Guid school, Guid vehicle, CancellationToken ct) => await s.FindAsync<Vehicle>(x => x.SchoolId == school && x.VehicleId == vehicle, ct) ?? throw VehicleErrors.Missing();
    private static async Task<VehicleResponse> Dto(IVehicleSession s, Vehicle v, CancellationToken ct) => new(v.VehicleId, v.SchoolId, v.BranchId, v.RegistrationCountryCode, v.RegistrationNumber, v.Manufacturer, v.Model, v.ManufactureYear, v.Transmission, v.Status, v.Revision, (await s.ListAsync<VehicleCategory>(x => x.SchoolId == v.SchoolId && x.VehicleId == v.VehicleId, ct)).Select(x => x.CategoryCode).Order(StringComparer.Ordinal).ToArray(), (await s.ListAsync<TeacherAssignment>(x => x.SchoolId == v.SchoolId && x.VehicleId == v.VehicleId, ct)).OrderBy(x => x.TeacherId).Select(x => new TeacherAssignmentResponse(x.TeacherId, x.SchoolMembershipId, x.AssignedAt)).ToArray());
    private static async Task EnsureNoSaga(IVehicleSession s, Vehicle v, CancellationToken ct)
    {
        if (await HasPendingStateChangeAsync(s, v.SchoolId, v.VehicleId, ct))
            throw VehicleErrors.Conflict("STATE_CHANGE_IN_PROGRESS");
    }

    private static async Task<bool> HasPendingStateChangeAsync(IVehicleSession session, Guid school, Guid vehicle, CancellationToken ct) =>
        (await session.ListAsync<StateChangeOperation>(x => x.SchoolId == school && x.VehicleId == vehicle &&
            x.Status != SagaStatus.Completed && x.Status != SagaStatus.Rejected, ct)).Count > 0;

    private static async Task<CommandRequest?> Replay(IVehicleSession s, Guid school, Guid actor, string key, string command, string fingerprint, CancellationToken ct)
    {
        var row = await s.FindAsync<CommandRequest>(x => x.SchoolId == school && x.ActorUserId == actor && x.CommandName == command && x.IdempotencyKey == key, ct);
        if (row is not null && row.Fingerprint != fingerprint)
            throw VehicleErrors.Conflict("IDEMPOTENCY_MISMATCH");
        return row;
    }

    private static MutationResponse Saved(CommandRequest row) => (JsonSerializer.Deserialize<MutationResponse>(row.ResponseJson!, VehicleRules.Json) ?? throw VehicleErrors.Dependency())with
    {
        Replayed = true
    };
    private static VehicleOperation Receipt(Guid operation, Vehicle v, VehicleActor actor, string command, string fingerprint, long? expected, DateTimeOffset now) => new()
    {
        OperationId = operation,
        SchoolId = v.SchoolId,
        VehicleId = v.VehicleId,
        ActorUserId = actor.UserId,
        Channel = Channel.Web,
        CommandName = command,
        RequestFingerprint = fingerprint,
        ExpectedVehicleRevision = expected,
        ResultingVehicleRevision = v.Revision,
        RecordedAt = now
    };
    private static async Task<MutationResponse> Complete(IVehicleSession s, Vehicle v, VehicleActor actor, string key, string command, string fingerprint, long? expected, CancellationToken ct)
    {
        var now = await s.NowAsync(ct);
        VehicleRules.Fresh(actor, now);
        var id = Guid.NewGuid();
        var response = new MutationResponse(id, await Dto(s, v, ct), false);
        s.Add(Receipt(id, v, actor, command, fingerprint, expected, now));
        s.Add(new CommandRequest { RequestId = id, SchoolId = v.SchoolId, ActorUserId = actor.UserId, IdempotencyKey = key, CommandName = command, Fingerprint = fingerprint, VehicleId = v.VehicleId, ResponseJson = JsonSerializer.Serialize(response, VehicleRules.Json), CreatedAt = now });
        await s.CommitAsync(ct);
        return response;
    }
}

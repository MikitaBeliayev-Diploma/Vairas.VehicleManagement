using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed partial class VehicleService
{
    public async Task<StateChangeResponse> RequestStateAsync(Guid school, Guid vehicle, string key, StateChangeRequest request, VehicleActor actor, string correlationId, CancellationToken ct)
    {
        key = VehicleRules.Key(key);
        VehicleRules.Id(vehicle);
        VehicleRules.Revision(request.ExpectedVehicleRevision);
        if (!Enum.IsDefined(request.OperationType))
            throw VehicleErrors.Invalid();
        request = request with
        {
            Reason = VehicleRules.Text(request.Reason, 1000)
        };
        var fingerprint = VehicleRules.Fingerprint("StateChange", school, vehicle, request);
        actor = await Authorize(school, null, actor, ct);
        await using var s = await store.OpenAsync(school, true, ct);
        var v = await Find(s, school, vehicle, ct);
        var prior = await Replay(s, school, actor.UserId, key, "StateChange", fingerprint, ct);
        if (prior is not null)
            return VehicleRules.Response(await s.FindAsync<StateChangeOperation>(x => x.SchoolId == school && x.OperationId == prior.RequestId, ct) ?? throw VehicleErrors.Dependency());
        if (!gate.Enabled)
            throw VehicleErrors.Dependency("SCHEDULING_INTEGRATION_NOT_READY");
        await EnsureNoSaga(s, v, ct);
        VehicleRules.Match(request.ExpectedVehicleRevision, v.Revision);
        if (request.OperationType == StateChangeType.Deactivate ? v.Status != VehicleStatus.Active : v.Status != VehicleStatus.Inactive)
            throw new UnprocessableEntityException("INVALID_STATE_TRANSITION");
        var now = await s.NowAsync(ct);
        VehicleRules.Fresh(actor, now);
        var operation = new StateChangeOperation
        {
            OperationId = Guid.NewGuid(),
            SchoolId = school,
            VehicleId = vehicle,
            BranchId = v.BranchId,
            ActorUserId = actor.UserId,
            OperationType = request.OperationType,
            Status = SagaStatus.Pending,
            ResumeStatus = SagaStatus.Pending,
            ExpectedVehicleRevision = v.Revision,
            Fingerprint = fingerprint,
            Reason = request.Reason,
            CorrelationId = VehicleRules.Text(correlationId, 128),
            ReleaseOperationId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
            NextAttemptAt = now
        };
        if (request.OperationType == StateChangeType.Reactivate)
        {
            var previous = (await s.ListAsync<StateChangeOperation>(x => x.SchoolId == school && x.VehicleId == vehicle && x.Status == SagaStatus.Completed, ct)).OrderByDescending(x => x.UpdatedAt).FirstOrDefault();
            if (previous is not null)
            {
                if (previous.OperationType != StateChangeType.Deactivate || previous.BlockId is null)
                    throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
                operation.BlockId = previous.BlockId;
                operation.BlockOperationId = previous.OperationId;
            }
            else if (await s.FindAsync<VehicleOperation>(x => x.SchoolId == school && x.VehicleId == vehicle && x.CommandName == "CreateVehicle", ct)is null)
                throw VehicleErrors.Dependency("VEHICLE_ORIGIN_NOT_VERIFIED");
        }

        s.Add(operation);
        s.Add(new CommandRequest { RequestId = operation.OperationId, SchoolId = school, ActorUserId = actor.UserId, CommandName = "StateChange", IdempotencyKey = key, Fingerprint = fingerprint, VehicleId = vehicle, CreatedAt = now });
        await s.CommitAsync(ct);
        return VehicleRules.Response(operation);
    }

    public async Task<StateChangeResponse> GetStateAsync(Guid school, Guid vehicle, Guid operation, VehicleActor actor, CancellationToken ct)
    {
        VehicleRules.Id(vehicle);
        VehicleRules.Id(operation);
        actor = await Authorize(school, null, actor, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var row = await s.FindAsync<StateChangeOperation>(x => x.SchoolId == school && x.VehicleId == vehicle && x.OperationId == operation, ct) ?? throw VehicleErrors.Missing();
        VehicleRules.Fresh(actor, await s.NowAsync(ct));
        return VehicleRules.Response(row);
    }
}

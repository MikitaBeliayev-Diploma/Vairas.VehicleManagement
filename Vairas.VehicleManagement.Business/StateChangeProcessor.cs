using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed class StateChangeProcessor(IVehicleStore store, ISchedulingGate gate, ISchoolAuthority authority, IUserProfiles users, ICategoryCatalogue catalogue, TimeProvider clock) : IStateChangeProcessor
{
    public async Task<bool> ProcessAsync(WorkItem item, CancellationToken ct)
    {
        if (!gate.Enabled)
            return false;
        StateChangeOperation work;
        var token = Guid.NewGuid();
        await using (var claim = await store.OpenAsync(item.SchoolId, true, ct))
        {
            var row = await claim.FindAsync<StateChangeOperation>(x => x.SchoolId == item.SchoolId && x.OperationId == item.ItemId, ct);
            var now = await claim.NowAsync(ct);
            if (row is null || VehicleRules.Terminal(row.Status) || row.NextAttemptAt > now || row.LeaseUntil > now)
                return false;
            row.LeaseToken = token;
            row.LeaseUntil = now.AddMinutes(2);
            row.Attempts++;
            row.UpdatedAt = now;
            await claim.CommitAsync(ct);
            // Sessions return detached values after disposal. Keep a stable work snapshot.
            work = JsonSerializer.Deserialize<StateChangeOperation>(JsonSerializer.Serialize(row))!;
        }

        var step = work.Status == SagaStatus.NeedsRecovery ? work.ResumeStatus : work.Status;
        using var activity = new Activity("vehicle.saga.transition").SetIdFormat(ActivityIdFormat.W3C).AddBaggage("correlationId", work.CorrelationId).Start();
        try
        {
            switch (step)
            {
                case SagaStatus.Pending:
                    await Save(item, token, (row, _, _) => row.Status = row.OperationType == StateChangeType.Deactivate ? SagaStatus.Blocking : SagaStatus.ApplyingVehicleState, ct);
                    break;
                case SagaStatus.Blocking:
                    await BlockSchedulingAsync(work, item, token, ct);
                    break;
                case SagaStatus.SchedulingBlocked:
                    await Save(item, token, (row, _, _) => row.Status = SagaStatus.ApplyingVehicleState, ct);
                    break;
                case SagaStatus.ApplyingVehicleState:
                    await ApplyVehicleStateAsync(work, item, token, ct);
                    break;
                case SagaStatus.ReleasingBlock:
                    await ReleaseSchedulingBlockAsync(work, item, token, ct);
                    break;
                default:
                    throw VehicleErrors.Dependency("UNKNOWN_SAGA_STATE");
            }

            VehicleTelemetry.SagaSteps.Add(1, new KeyValuePair<string, object?>("step", step.ToString()));
        }
        catch (Exception e)when (!ct.IsCancellationRequested)
        {
            await HandleFailureAsync(work, item, token, step, e, ct);
        }

        return true;
    }

    private static void CheckBlock(StateChangeOperation work, BlockResult result)
    {
        if (result.OperationId != work.OperationId || result.VehicleId != work.VehicleId || result.Status is not ("Active" or "Rejected") || result.Status == "Active" && (result.BlockId is null || result.BlockId == Guid.Empty) || result.Status == "Rejected" && result.BlockId.HasValue)
            throw VehicleErrors.Dependency("INVALID_BLOCK_OUTCOME");
    }

    private async Task Save(WorkItem item, Guid lease, Action<StateChangeOperation, IVehicleSession, DateTimeOffset> action, CancellationToken ct)
    {
        await using var s = await store.OpenAsync(item.SchoolId, true, ct);
        var row = await s.FindAsync<StateChangeOperation>(x => x.SchoolId == item.SchoolId && x.OperationId == item.ItemId, ct) ?? throw VehicleErrors.Missing();
        var now = await s.NowAsync(ct);
        if (row.LeaseToken != lease || row.LeaseUntil <= now)
            return;
        action(row, s, now);
        row.UpdatedAt = now;
        row.LeaseToken = null;
        row.LeaseUntil = null;
        if (row.Status != SagaStatus.NeedsRecovery)
        {
            row.NextAttemptAt = now;
            if (row.Status != SagaStatus.Rejected)
                row.ErrorCode = null;
        }

        await s.CommitAsync(ct);
    }

    private async Task Apply(WorkItem item, Guid lease, StateChangeOperation basis, DateTimeOffset validUntil, CancellationToken ct)
    {
        await using var s = await store.OpenAsync(item.SchoolId, true, ct);
        var row = await s.FindAsync<StateChangeOperation>(x => x.SchoolId == item.SchoolId && x.OperationId == item.ItemId, ct) ?? throw VehicleErrors.Missing();
        var now = await s.NowAsync(ct);
        if (row.LeaseToken != lease || row.LeaseUntil <= now)
            return;
        if (validUntil <= now)
            throw VehicleErrors.Dependency();
        var vehicle = await s.FindAsync<Vehicle>(x => x.SchoolId == item.SchoolId && x.VehicleId == row.VehicleId, ct) ?? throw VehicleErrors.Missing();
        VehicleRules.Match(basis.ExpectedVehicleRevision, vehicle.Revision);
        var categories = await s.ListAsync<VehicleCategory>(x => x.SchoolId == item.SchoolId && x.VehicleId == row.VehicleId, ct);
        VehicleRules.Categories(categories.Select(x => x.CategoryCode).ToArray(), catalogue);
        var expected = row.OperationType == StateChangeType.Deactivate ? VehicleStatus.Active : VehicleStatus.Inactive;
        if (vehicle.Status != expected)
            throw new UnprocessableEntityException("INVALID_STATE_TRANSITION");
        vehicle.Status = row.OperationType == StateChangeType.Deactivate ? VehicleStatus.Inactive : VehicleStatus.Active;
        vehicle.Revision++;
        vehicle.UpdatedAt = now;
        row.ResultVehicleStatus = vehicle.Status;
        row.Status = row.OperationType == StateChangeType.Reactivate && row.BlockId.HasValue ? SagaStatus.ReleasingBlock : SagaStatus.Completed;
        row.LeaseToken = null;
        row.LeaseUntil = null;
        row.NextAttemptAt = now;
        row.UpdatedAt = now;
        row.ErrorCode = null;
        s.Add(new VehicleOperation { OperationId = row.OperationId, SchoolId = row.SchoolId, VehicleId = row.VehicleId, ActorUserId = row.ActorUserId, Channel = Channel.Web, CommandName = row.OperationType.ToString(), RequestFingerprint = row.Fingerprint, ExpectedVehicleRevision = row.ExpectedVehicleRevision, ResultingVehicleRevision = vehicle.Revision, Reason = row.Reason, RecordedAt = now });
        var eventType = vehicle.Status == VehicleStatus.Active ? "VehicleActivated" : "VehicleDeactivated";
        var payload = vehicle.Status == VehicleStatus.Active ? JsonSerializer.Serialize(new { schoolId = row.SchoolId, branchId = vehicle.BranchId, vehicleId = row.VehicleId, effectiveAt = now }, VehicleRules.Json) : JsonSerializer.Serialize(new { schoolId = row.SchoolId, branchId = vehicle.BranchId, vehicleId = row.VehicleId, effectiveAt = now, reason = row.Reason }, VehicleRules.Json);
        s.Add(new OutboxMessage { MessageId = Guid.NewGuid(), SchoolId = row.SchoolId, VehicleId = row.VehicleId, EventType = eventType, AggregateVersion = vehicle.Revision, PayloadJson = payload, CorrelationId = row.CorrelationId, OccurredAt = now, NextAttemptAt = now });
        await s.CommitAsync(ct);
    }

    private async Task BlockSchedulingAsync(StateChangeOperation work, WorkItem item, Guid token, CancellationToken ct)
    {
        var result = await gate.OutcomeAsync(work.SchoolId, work.OperationId, ct) ?? await gate.BlockAsync(work.SchoolId, work.OperationId, work.VehicleId, ct);
        CheckBlock(work, result);
        await Save(item, token, (row, _, _) =>
        {
            if (result.Status == "Rejected")
            {
                row.Status = SagaStatus.Rejected;
                row.ErrorCode = result.ErrorCode ?? "SCHEDULING_CONFLICT";
            }
            else
            {
                row.BlockId = result.BlockId;
                row.BlockOperationId = work.OperationId;
                row.Status = SagaStatus.SchedulingBlocked;
            }
        }, ct);
    }

    private async Task ApplyVehicleStateAsync(StateChangeOperation work, WorkItem item, Guid token, CancellationToken ct)
    {
        var query = new VehicleAuthorityRequest(work.SchoolId, work.BranchId, work.ActorUserId);
        await users.RequireActiveAsync(work.ActorUserId, ct);
        var validUntil = VehicleRules.Authority(query, await authority.ValidateAsync(query, ct), clock.GetUtcNow());
        if (work.OperationType == StateChangeType.Deactivate || work.BlockId.HasValue)
        {
            if (work.BlockOperationId is not Guid owningOperation)
                throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
            var block = await gate.OutcomeAsync(work.SchoolId, owningOperation, ct);
            if (block?.Status != "Active" || block.BlockId != work.BlockId || block.OperationId != owningOperation || block.VehicleId != work.VehicleId)
                throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
        }

        await Apply(item, token, work, validUntil, ct);
    }

    private async Task ReleaseSchedulingBlockAsync(StateChangeOperation work, WorkItem item, Guid token, CancellationToken ct)
    {
        if (work.BlockId is not Guid blockId || work.BlockOperationId is not Guid blockOperation)
            throw VehicleErrors.Dependency("BLOCK_NOT_VERIFIED");
        var release = await gate.ReleaseAsync(work.SchoolId, blockId, blockOperation, work.ReleaseOperationId, ct);
        if (release.Status != "Released" || release.BlockId != blockId || release.VehicleId != work.VehicleId || release.OperationId != work.ReleaseOperationId)
            throw VehicleErrors.Dependency("BLOCK_RELEASE_UNCERTAIN");
        await Save(item, token, (row, _, _) => row.Status = SagaStatus.Completed, ct);
    }

    private async Task HandleFailureAsync(StateChangeOperation work, WorkItem item, Guid token, SagaStatus step, Exception e, CancellationToken ct)
    {
        var code = e is BusinessException ve ? ve.Code : "DEPENDENCY_UNAVAILABLE";
        await Save(item, token, (row, _, now) =>
        {
            // Rejection is only safe before a remote block may have been applied.
            var safeReject = work.OperationType == StateChangeType.Reactivate && work.BlockId is null && step == SagaStatus.ApplyingVehicleState && e is ForbiddenException or UnprocessableEntityException;
            row.Status = safeReject ? SagaStatus.Rejected : SagaStatus.NeedsRecovery;
            row.ResumeStatus = step;
            row.ErrorCode = code;
            row.NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(row.Attempts, 8))) + Random.Shared.Next(0, 5));
        }, ct);
        VehicleTelemetry.RecoveryAttempts.Add(1, new KeyValuePair<string, object?>("code", code));
    }
}

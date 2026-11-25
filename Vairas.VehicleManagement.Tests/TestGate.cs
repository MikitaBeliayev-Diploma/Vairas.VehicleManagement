using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class TestGate : ISchedulingGate
{
    public bool Enabled { get; set; } = true;
    public bool Reject { get; set; }
    public bool ThrowAfterBlock { get; set; }
    public bool Unavailable { get; set; }
    public bool BadRelease { get; set; }
    public int Blocks { get; private set; }
    public int Releases { get; private set; }
    public Dictionary<Guid, BlockResult> Outcomes { get; } = [];
    public Task<BlockResult?> OutcomeAsync(Guid operationId, CancellationToken ct)
    { if (Unavailable) throw VehicleErrors.Dependency(); return Task.FromResult(Outcomes.GetValueOrDefault(operationId)); }
    public Task<BlockResult> BlockAsync(Guid operationId, Guid vehicleId, CancellationToken ct)
    {
        if (Unavailable) throw VehicleErrors.Dependency();
        Blocks++;
        var result = new BlockResult(operationId, vehicleId, Reject ? "Rejected" : "Active", Reject ? null : Guid.NewGuid(), Reject ? "FUTURE_LESSONS_EXIST" : null);
        Outcomes[operationId] = result;
        if (ThrowAfterBlock) { ThrowAfterBlock = false; throw VehicleErrors.Dependency(); }
        return Task.FromResult(result);
    }
    public Task<BlockResult> ReleaseAsync(Guid blockId, Guid blockOperationId, Guid releaseOperationId, CancellationToken ct)
    {
        if (Unavailable) throw VehicleErrors.Dependency();
        Releases++;
        var previous = Outcomes[blockOperationId];
        var result = new BlockResult(releaseOperationId, previous.VehicleId, BadRelease ? "Active" : "Released", blockId, null);
        Outcomes[blockOperationId] = previous with { Status = "Released" };
        return Task.FromResult(result);
    }
}

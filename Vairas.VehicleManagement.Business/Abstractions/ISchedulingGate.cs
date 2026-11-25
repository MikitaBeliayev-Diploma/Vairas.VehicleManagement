using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface ISchedulingGate
{
    Task<BlockResult?> OutcomeAsync(Guid school,Guid operation,CancellationToken ct) => OutcomeAsync(operation,ct);
    Task<BlockResult> BlockAsync(Guid school,Guid operation,Guid vehicle,CancellationToken ct) => BlockAsync(operation,vehicle,ct);
    Task<BlockResult> ReleaseAsync(Guid school,Guid block,Guid owningOperation,Guid releaseOperation,CancellationToken ct) => ReleaseAsync(block,owningOperation,releaseOperation,ct);
    bool Enabled { get; }

    Task<BlockResult?> OutcomeAsync(Guid operationId, CancellationToken ct);
    Task<BlockResult> BlockAsync(Guid operationId, Guid vehicleId, CancellationToken ct);
    Task<BlockResult> ReleaseAsync(Guid blockId, Guid blockOperationId, Guid releaseOperationId, CancellationToken ct);
}

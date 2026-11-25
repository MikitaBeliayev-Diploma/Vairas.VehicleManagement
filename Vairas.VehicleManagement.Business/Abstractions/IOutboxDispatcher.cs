using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface IOutboxDispatcher
{
    Task<bool> DispatchAsync(WorkItem item, CancellationToken ct);
}

using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface IStateChangeProcessor
{
    Task<bool> ProcessAsync(WorkItem item, CancellationToken ct);
}

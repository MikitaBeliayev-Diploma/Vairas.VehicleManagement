using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public interface IVehicleStore
{
    Task<IVehicleSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct);
    Task<IReadOnlyList<WorkItem>> DueAsync(bool outbox, int take, CancellationToken ct);
    Task<WorkHealth> HealthAsync(CancellationToken ct);
}

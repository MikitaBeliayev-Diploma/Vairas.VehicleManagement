using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface IVehicleSession : IAsyncDisposable
{
    Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct)
        where T : class;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct)
        where T : class;
    Task<IReadOnlyList<Vehicle>> PageAsync(Guid schoolId, Guid? after, int take, CancellationToken ct);
    Task<DateTimeOffset> NowAsync(CancellationToken ct);
    void Add<T>(T entity)
        where T : class;
    void Remove<T>(T entity)
        where T : class;
    Task CommitAsync(CancellationToken ct);
}

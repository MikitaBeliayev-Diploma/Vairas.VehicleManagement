using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class MemoryStore(FixedClock clock) : IVehicleStore
{
    public Task<WorkHealth> HealthAsync(CancellationToken ct) => Task.FromResult(new WorkHealth(Rows.OfType<StateChangeOperation>().Count(x => !VehicleRules.Terminal(x.Status)),
        Rows.OfType<StateChangeOperation>().Count(x => x.Status == SagaStatus.NeedsRecovery), 0, Rows.OfType<OutboxMessage>().Count(x => x.PublishedAt is null), 0));
    public List<object> Rows { get; set; } = [];
    public int Commits { get; private set; }
    private readonly SemaphoreSlim mutex = new(1, 1);
    public async Task<IVehicleSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        return new Session(this, schoolId, clock);
    }
    public Task<IReadOnlyList<WorkItem>> DueAsync(bool outbox, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkItem>>(outbox ? Rows.OfType<OutboxMessage>().Where(x => x.PublishedAt is null && x.NextAttemptAt <= clock.Now && !(x.LeaseUntil > clock.Now)).Take(take).Select(x => new WorkItem(x.SchoolId, x.MessageId)).ToArray()
        : Rows.OfType<StateChangeOperation>().Where(x => !VehicleRules.Terminal(x.Status) && x.NextAttemptAt <= clock.Now && !(x.LeaseUntil > clock.Now)).Take(take).Select(x => new WorkItem(x.SchoolId, x.OperationId)).ToArray());
    private sealed class Session : IVehicleSession
    {
        private readonly MemoryStore store; private readonly Guid school; private readonly FixedClock clock; private readonly List<object> rows;
        public Session(MemoryStore store, Guid school, FixedClock clock)
        { this.store = store; this.school = school; this.clock = clock; rows = store.Rows.Select(Clone).ToList(); }
        private static object Clone(object x) => JsonSerializer.Deserialize(JsonSerializer.Serialize(x, x.GetType()), x.GetType())!;
        private IEnumerable<T> Scoped<T>() where T : class => rows.OfType<T>().Where(x => (Guid)x.GetType().GetProperty("SchoolId")!.GetValue(x)! == school);
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => Task.FromResult(Scoped<T>().SingleOrDefault(predicate.Compile()));
        public Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => Task.FromResult<IReadOnlyList<T>>(Scoped<T>().Where(predicate.Compile()).ToArray());
        public Task<IReadOnlyList<Vehicle>> PageAsync(Guid schoolId, Guid? after, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Vehicle>>(Scoped<Vehicle>().Where(x => after is null || x.VehicleId.CompareTo(after.Value) > 0).OrderBy(x => x.VehicleId).Take(take).ToArray());
        public Task<DateTimeOffset> NowAsync(CancellationToken ct) => Task.FromResult(clock.Now);
        public void Add<T>(T entity) where T : class => rows.Add(entity);
        public void Remove<T>(T entity) where T : class => rows.Remove(entity);
        public Task CommitAsync(CancellationToken ct) { store.Rows = rows.Select(Clone).ToList(); store.Commits++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { store.mutex.Release(); return ValueTask.CompletedTask; }
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance;

public sealed class VehicleStore(IDbContextFactory<VehicleDbContext> factory) : IVehicleStore
{
    public async Task<WorkHealth> HealthAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Database.SqlQueryRaw<WorkHealth>("SELECT * FROM public.vm_work_health()").SingleAsync(ct);
    }
    public Task<IVehicleSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => OpenCoreAsync(schoolId, write, ct));
    private async Task<IVehicleSession> OpenCoreAsync(Guid schoolId, bool write, CancellationToken ct)
    {
        var db = await factory.CreateDbContextAsync(ct);
        try
        {
            var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('vairas.school_id',{schoolId.ToString("D")},true)", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='5s'", ct);
            // A tenant-scoped advisory lock also serializes creation and idempotency claims
            // before a vehicle row exists. All writers, workers included, take this lock.
            if (write) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({schoolId.ToString("D")},731))", ct);
            else db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return new Session(db, transaction);
        }
        catch { await db.DisposeAsync(); throw; }
    }
    public async Task<IReadOnlyList<WorkItem>> DueAsync(bool outbox, int take, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Narrow SECURITY DEFINER discovery returns only work identifiers, never business payloads.
        return await db.Database.SqlQuery<WorkItem>($"SELECT school_id AS \"SchoolId\", item_id AS \"ItemId\" FROM public.vm_due_work({outbox},{take})").ToArrayAsync(ct);
    }
    private sealed class Session(VehicleDbContext db, IDbContextTransaction transaction) : IVehicleSession
    {
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => DatabaseExceptionTranslator.GuardAsync(() => db.Set<T>().SingleOrDefaultAsync(predicate, ct));
        public Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => DatabaseExceptionTranslator.GuardAsync(() => ListCoreAsync(predicate, ct));
        private async Task<IReadOnlyList<T>> ListCoreAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class
        {
            var rows = await db.Set<T>().Where(predicate).ToListAsync(ct);
            var matches = predicate.Compile();
            foreach (var entry in db.ChangeTracker.Entries<T>())
            {
                if (entry.State == EntityState.Deleted) rows.Remove(entry.Entity);
                else if (entry.State == EntityState.Added && matches(entry.Entity) && !rows.Contains(entry.Entity)) rows.Add(entry.Entity);
            }
            return rows;
        }
        public async Task<IReadOnlyList<Vehicle>> PageAsync(Guid schoolId, Guid? after, int take, CancellationToken ct)
        {
            var vehicles = db.Set<Vehicle>().AsNoTracking().Where(vehicle => vehicle.SchoolId == schoolId);
            if (after is { } cursor)
                vehicles = vehicles.Where(vehicle => vehicle.VehicleId.CompareTo(cursor) > 0);
            return await vehicles.OrderBy(vehicle => vehicle.VehicleId).Take(take).ToArrayAsync(ct);
        }
        public Task<DateTimeOffset> NowAsync(CancellationToken ct) => db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
        public void Add<T>(T entity) where T : class => db.Add(entity);
        public void Remove<T>(T entity) where T : class => db.Remove(entity);
        public Task CommitAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(async () => { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); });
        public async ValueTask DisposeAsync()
        { await transaction.DisposeAsync(); await db.DisposeAsync(); }
    }
}

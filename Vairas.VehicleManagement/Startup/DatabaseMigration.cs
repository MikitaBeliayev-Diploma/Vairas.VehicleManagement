using Microsoft.EntityFrameworkCore;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.Startup;

/// <summary>The `--migrate` command: applies EF migrations and runtime grants as the migration role, then exits.</summary>
public static class DatabaseMigration
{
    public static async Task MigrateAsync()
    {
        var connection = Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION")
            ?? throw new InvalidOperationException("Supply VAIRAS_MIGRATION_CONNECTION.");
        await using var db = new VehicleDbContext(new DbContextOptionsBuilder<VehicleDbContext>().UseVehicle(connection).Options);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database/runtime-grants.sql")));
    }
}

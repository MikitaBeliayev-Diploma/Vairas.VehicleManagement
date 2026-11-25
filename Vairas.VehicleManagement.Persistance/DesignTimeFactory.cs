using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<VehicleDbContext>
{
    public VehicleDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<VehicleDbContext>().UseVehicle(Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION") ?? "Host=localhost;Database=vairas_vehicle_management;Username=v_vehicle_management_migration").Options);
}

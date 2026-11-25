using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance;
public static class VehicleDatabase
{
    private static readonly NpgsqlNullNameTranslator Names = new();
    public static DbContextOptionsBuilder<VehicleDbContext> UseVehicle(this DbContextOptionsBuilder<VehicleDbContext> builder, string connection)
    {
        Configure(builder, connection);
        return builder;
    }

    public static void Configure(DbContextOptionsBuilder builder, string connection) => builder.UseNpgsql(connection, o =>
    {
        o.MapEnum<VehicleStatus>("vm_vehicle_status", nameTranslator: Names);
        o.MapEnum<Transmission>("vm_transmission", nameTranslator: Names);
        o.MapEnum<MaintenanceType>("vm_maintenance_type", nameTranslator: Names);
        o.MapEnum<MaintenanceStatus>("vm_maintenance_status", nameTranslator: Names);
        o.MapEnum<Channel>("vm_channel", nameTranslator: Names);
    });
}

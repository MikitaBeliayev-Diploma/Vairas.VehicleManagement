using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vairas.VehicleManagement.Integrations;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.HealthChecks;
public sealed class DatabaseReadiness(IDbContextFactory<VehicleDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var valid = await db.Database.SqlQueryRaw<bool>("""
                SELECT (NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb AND NOT r.rolcreaterole
                 AND NOT has_schema_privilege('public','CREATE') AND NOT has_database_privilege(current_database(),'TEMP')
                 AND (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND rowsecurity AND tablename IN
                 ('vehicle','vehicle_supported_category','vehicle_teacher_assignment','maintenance','vehicle_operation','command_request','vehicle_state_change_operation','outbox_message'))=8
                 AND NOT EXISTS(SELECT 1 FROM pg_tables WHERE schemaname='public' AND tableowner=current_user)
                 AND to_regprocedure('public.vm_due_work(boolean,integer)') IS NOT NULL
                 AND to_regprocedure('public.vm_work_health()') IS NOT NULL) AS "Value"
                FROM pg_roles r WHERE r.rolname=current_user
                """).SingleAsync(ct);
            return valid ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Schema, isolation or runtime role is not ready.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}

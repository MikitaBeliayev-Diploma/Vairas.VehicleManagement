using Npgsql;

namespace Vairas.VehicleManagement.Startup;

public static class DatabaseProvisioning
{
    public static async Task ProvisionAsync()
    {
        var admin = Environment.GetEnvironmentVariable("VAIRAS_PROVISION_CONNECTION") ?? throw new InvalidOperationException("Supply VAIRAS_PROVISION_CONNECTION.");
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        foreach (var (role, variable) in new[] { ("v_vehicle_management_migration", "VAIRAS_MIGRATION_PASSWORD"), ("v_vehicle_management_runtime", "VAIRAS_RUNTIME_PASSWORD") })
        {
            var password = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Supply database passwords externally.");
            await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role)", connection);
            exists.Parameters.AddWithValue("role", role);
            if (!(bool)(await exists.ExecuteScalarAsync())!)
                await using (var create = new NpgsqlCommand($"CREATE ROLE {role} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS", connection)) await create.ExecuteNonQueryAsync();
            await using var quote = new NpgsqlCommand("SELECT format('ALTER ROLE %I LOGIN PASSWORD %L',@role,@password)", connection);
            quote.Parameters.AddWithValue("role", role); quote.Parameters.AddWithValue("password", password);
            var sql = (string)(await quote.ExecuteScalarAsync())!;
            await using var change = new NpgsqlCommand(sql, connection); await change.ExecuteNonQueryAsync();
        }
        await using (var grant = new NpgsqlCommand("GRANT v_vehicle_management_migration TO CURRENT_USER", connection)) await grant.ExecuteNonQueryAsync();
        await using var databaseExists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname='vairas_vehicle_management')", connection);
        if (!(bool)(await databaseExists.ExecuteScalarAsync())!)
        {
            await using var createDatabase = new NpgsqlCommand("CREATE DATABASE vairas_vehicle_management OWNER v_vehicle_management_migration", connection);
            await createDatabase.ExecuteNonQueryAsync();
        }
    }
}

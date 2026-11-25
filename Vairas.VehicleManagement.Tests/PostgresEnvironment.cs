using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;
using Vairas.VehicleManagement.Integrations;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.Tests;

public sealed class PostgresEnvironment : IAsyncDisposable
{
    private PostgreSqlContainer? container;
    private string adminConnection = "", database = "", ownerRole = "", runtimeRole = "";
    private readonly string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private readonly RSA rsa = RSA.Create(2048);
    public FixedClock Clock { get; } = new() { Now = DateTimeOffset.UtcNow };
    public Guid School { get; } = Guid.NewGuid();
    public Guid Branch { get; } = Guid.NewGuid();
    public Guid Administrator { get; } = Guid.NewGuid();
    public string OwnerConnection { get; private set; } = "";
    public string RuntimeConnection { get; private set; } = "";
    public string ServerVersion { get; private set; } = "";
    public TestAuthority Authority { get; }
    public TestUsers Users { get; } = new();
    public TestGate Gate { get; } = new() { Enabled = false };
    public VehicleFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public PostgresEnvironment() => Authority = new(Clock);
    public static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
    public VehicleDbContext Db(bool owner = false) => new(new DbContextOptionsBuilder<VehicleDbContext>().UseVehicle(owner ? OwnerConnection : RuntimeConnection).Options);
    public async Task StartAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("VAIRAS_TEST_ADMIN_CONNECTION") ?? "";
        if (adminConnection.Length == 0)
        {
            container = new PostgreSqlBuilder("postgres:17").WithPassword(password).Build();
            await container.StartAsync(); adminConnection = container.GetConnectionString();
        }
        var suffix = Guid.NewGuid().ToString("N");
        database = "vm_test_" + suffix; ownerRole = "vm_owner_" + suffix; runtimeRole = "vm_runtime_" + suffix;
        await Sql(adminConnection, $"CREATE ROLE {ownerRole} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; CREATE ROLE {runtimeRole} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
        await Sql(adminConnection, $"CREATE DATABASE {database} OWNER {ownerRole}");
        OwnerConnection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = database, Username = ownerRole, Password = password, Pooling = false }.ConnectionString;
        RuntimeConnection = new NpgsqlConnectionStringBuilder(OwnerConnection) { Username = runtimeRole }.ConnectionString;
        await using (var db = Db(true))
        {
            await db.Database.MigrateAsync();
            await db.Database.OpenConnectionAsync(); await ((NpgsqlConnection)db.Database.GetDbConnection()).ReloadTypesAsync();
        }
        await ApplyGrantsAsync();
        ServerVersion = await Scalar(OwnerConnection, "SELECT version()");
        var key = new RsaSecurityKey(rsa) { KeyId = "vehicle-integration-test" };
        Factory = new(this, key); Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
    }
    public Task ApplyGrantsAsync() => Apply();
    private async Task Apply() => await Sql(OwnerConnection, (await File.ReadAllTextAsync(Path.Combine(Root, "database/runtime-grants.sql"))).Replace("v_vehicle_management_runtime", runtimeRole).Replace("v_vehicle_management_migration", ownerRole));
    public string Token(bool workload = false, string? audience = null, string scope = "vehicles.eligibility.read", string client = "scheduling")
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "vehicle-integration-test" }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken("https://identity.test", audience ?? (workload ? "vairas-vehicle-management-internal" : "vairas-vehicle-management"),
            [new Claim("sub", workload ? client : Administrator.ToString("D")), new Claim("sid", Guid.NewGuid().ToString("D")), new Claim("scope", scope)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public static async Task Sql(string connection, string sql)
    { await using var c = new NpgsqlConnection(connection); await c.OpenAsync(); await using var command = new NpgsqlCommand(sql, c); await command.ExecuteNonQueryAsync(); }
    public static async Task<string> Scalar(string connection, string sql)
    { await using var c = new NpgsqlConnection(connection); await c.OpenAsync(); await using var command = new NpgsqlCommand(sql, c); return (await command.ExecuteScalarAsync())?.ToString() ?? ""; }
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose(); if (Factory is not null) await Factory.DisposeAsync(); NpgsqlConnection.ClearAllPools();
        if (database.Length > 0) await Sql(adminConnection, $"DROP DATABASE IF EXISTS {database} WITH (FORCE)");
        if (ownerRole.Length > 0) await Sql(adminConnection, $"DROP ROLE IF EXISTS {runtimeRole}; DROP ROLE IF EXISTS {ownerRole}");
        if (container is not null) await container.DisposeAsync(); rsa.Dispose();
    }
}

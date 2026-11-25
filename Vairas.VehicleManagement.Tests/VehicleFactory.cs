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

public sealed class VehicleFactory(PostgresEnvironment environment, SecurityKey key) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:VehicleManagement"] = environment.RuntimeConnection,
            ["Dependencies:ClientSecret"] = "synthetic-test-credential",
            ["Authentication:Authority"] = "https://identity.test", ["Authentication:Issuer"] = "https://identity.test",
            ["Authentication:WorkloadClients:scheduling:0"] = "vehicles.eligibility.read", ["Workers:Enabled"] = "false",
            ["Catalogue:Path"] = Path.Combine(PostgresEnvironment.Root, "Vairas.VehicleManagement/catalogue/demo-categories.json")
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISchoolAuthority>(); services.AddSingleton<ISchoolAuthority>(environment.Authority);
            services.RemoveAll<IUserProfiles>(); services.AddSingleton<IUserProfiles>(environment.Users);
            services.RemoveAll<ISchedulingGate>(); services.AddSingleton<ISchedulingGate>(environment.Gate);
            services.RemoveAll<IVehicleActorFactory>(); services.AddSingleton<IVehicleActorFactory>(new ActorFactory(environment));
            foreach (var scheme in new[] { "User", "Workload" })
                services.Configure<JwtBearerOptions>(scheme, o =>
                {
                    o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { Issuer = "https://identity.test", SigningKeys = { key } });
                });
        });
    }
    private sealed class ActorFactory(PostgresEnvironment environment) : IVehicleActorFactory
    {
        public Task<VehicleActor> CreateAsync(Microsoft.AspNetCore.Http.HttpContext http, CancellationToken ct) =>
            Task.FromResult(new VehicleActor(environment.Administrator, DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}

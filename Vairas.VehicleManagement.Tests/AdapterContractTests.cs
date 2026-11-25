using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Configuration;
using Vairas.VehicleManagement.Integrations;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Tests;

[TestFixture, Category("Contract")]
public sealed class AdapterContractTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    private sealed class Clients(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    [Test] public async Task OAuth_wire_names_are_parsed_and_token_is_cached()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            requests++; Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/connect/token"));
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.That(body, Does.Contain("grant_type=client_credentials"));
            return Json("{\"access_token\":\"synthetic-token\",\"token_type\":\"Bearer\",\"expires_in\":300}");
        }));
        using var tokens = new WorkloadTokens(new Clients(client), Options.Create(new DependencyOptions { ClientSecret = "synthetic" }), new FixedClock());
        Assert.That(await tokens.GetAsync(default), Is.EqualTo("synthetic-token"));
        Assert.That(await tokens.GetAsync(default), Is.EqualTo("synthetic-token")); Assert.That(requests, Is.EqualTo(1));
    }
    private sealed class Transport(Guid identity, Guid session, Guid user, DateTimeOffset until) : IDependencyTransport
    {
        public bool BadSession { get; set; }
        public bool Active { get; set; } = true;
        public Task<HttpResponseMessage> RequestAsync(string origin, string route, object? body, CancellationToken ct, string? userToken = null) => throw new NotSupportedException();
        public Task<T> SendAsync<T>(string origin, string route, object? body, CancellationToken ct, string? userToken = null)
        {
            var json = route.EndsWith("sessions/validate", StringComparison.Ordinal)
                ? System.Text.Json.JsonSerializer.Serialize(new { active = Active, identityId = BadSession ? Guid.NewGuid() : identity, sessionId = session, validUntil = until })
                : System.Text.Json.JsonSerializer.Serialize(new { userId = user, status = "Active" });
            if (route.EndsWith("users/me", StringComparison.Ordinal)) Assert.That(userToken, Is.EqualTo("synthetic-user-token"));
            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json, VehicleRules.Json)!);
        }
    }
    private sealed class Authentication(ClaimsPrincipal principal) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.Success(new(principal, "User")));
        public Task ChallengeAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task SignInAsync(HttpContext c, string? s, ClaimsPrincipal u, AuthenticationProperties? p) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
    }
    [Test] public async Task Canonical_UserId_is_resolved_instead_of_treating_IdentityId_as_UserId()
    {
        var clock = new FixedClock(); var identity = Guid.NewGuid(); var session = Guid.NewGuid(); var user = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", identity.ToString()), new Claim("sid", session.ToString())], "User"));
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication(principal)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services }; http.Request.Headers.Authorization = "Bearer synthetic-user-token";
        var transport = new Transport(identity, session, user, clock.Now.AddMinutes(5));
        var factory = new VehicleActorFactory(transport, Options.Create(new DependencyOptions()), clock);
        Assert.That((await factory.CreateAsync(http, default)).UserId, Is.EqualTo(user));
        transport.BadSession = true;
        Assert.That(Business.ThrowsBusinessAsync(async () => await factory.CreateAsync(http, default)).HttpStatus(), Is.EqualTo(503));
        transport.BadSession = false; transport.Active = false;
        Assert.That(Business.ThrowsBusinessAsync(async () => await factory.CreateAsync(http, default)).HttpStatus(), Is.EqualTo(401));
    }
    [Test] public void Production_rejects_synthetic_catalogue_while_testing_can_use_it()
    {
        var config = Options.Create(new CatalogueOptions { Path = Path.Combine(PostgresEnvironment.Root, "Vairas.VehicleManagement/catalogue/demo-categories.json") });
        Assert.Throws<InvalidOperationException>(() => new CategoryCatalogue(config, new Environment("Production")));
        var testing = new CategoryCatalogue(config, new Environment("Testing"));
        Assert.That(testing.Contains("B"), Is.True); Assert.That(testing.Contains("UNKNOWN"), Is.False);
    }
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = PostgresEnvironment.Root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

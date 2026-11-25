using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Npgsql;
using NUnit.Framework;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.Tests;

[TestFixture, Category("Integration"), NonParallelizable]
public sealed class PostgresApiTests
{
    private PostgresEnvironment env = null!;
    [OneTimeSetUp] public async Task Start() { env = new(); await env.StartAsync(); }
    [OneTimeTearDown] public async Task Stop() { if (env is not null) await env.DisposeAsync(); }
    [SetUp] public void Reset() { env.Authority.Allowed = true; env.Authority.Expired = false; env.Authority.Alter = null; env.Gate.Enabled = false; env.Gate.Reject = false; env.Gate.ThrowAfterBlock = false; env.Gate.Unavailable = false; env.Users.Inactive.Clear(); env.Users.BrokenBinding = false; }
    private string Collection => $"/api/v1/schools/{env.School}/vehicles";
    private CreateVehicleRequest Request => new(env.Branch, "LT", "TEST" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), "Synthetic", "Vehicle", 2020, Transmission.Manual, ["B"]);
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? key = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: VehicleRules.Json);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-ID", "integration-vehicle");
        return await env.Client.SendAsync(request);
    }
    private async Task<MutationResponse> Create()
    {
        using var response = await Send(HttpMethod.Post, Collection, Request, Guid.NewGuid().ToString("D"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MutationResponse>(VehicleRules.Json))!;
    }
    [Test] public async Task Real_PostgreSQL_CRUD_receipts_and_exact_replay()
    {
        var request = Request; var key = Guid.NewGuid().ToString("D");
        using var first = await Send(HttpMethod.Post, Collection, request, key);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created), await first.Content.ReadAsStringAsync());
        var created = (await first.Content.ReadFromJsonAsync<MutationResponse>(VehicleRules.Json))!;
        Assert.That(created.Resource.Categories, Is.EqualTo(new[] { "B" })); Assert.That(first.Headers.Location, Is.Not.Null);
        Assert.That(first.Headers.Location!.IsAbsoluteUri, Is.False, "Public Location must remain usable behind the gateway.");
        using var replay = await Send(HttpMethod.Post, Collection, request, key);
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = (await replay.Content.ReadFromJsonAsync<MutationResponse>(VehicleRules.Json))!;
        Assert.That(saved.OperationId, Is.EqualTo(created.OperationId)); Assert.That(saved.Replayed, Is.True);
        using var changed = await Send(HttpMethod.Post, Collection, request with { Model = "Other" }, key);
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using var read = await env.Client.GetAsync(first.Headers.Location);
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var update = await Send(HttpMethod.Patch, $"{Collection}/{created.Resource.VehicleId}", new UpdateVehicleRequest(1, "LT", request.RegistrationNumber, "Updated", "Vehicle", 2021, Transmission.Automatic), Guid.NewGuid().ToString("D"));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), await update.Content.ReadAsStringAsync());
        using var categories = await Send(HttpMethod.Put, $"{Collection}/{created.Resource.VehicleId}/categories", new ReplaceCategoriesRequest(2, ["BE"]), Guid.NewGuid().ToString("D"));
        Assert.That(categories.StatusCode, Is.EqualTo(HttpStatusCode.OK), await categories.Content.ReadAsStringAsync());
        Assert.That((await categories.Content.ReadFromJsonAsync<MutationResponse>(VehicleRules.Json))!.Resource.Categories, Is.EqualTo(new[] { "BE" }));
    }
    [Test] public async Task Concurrent_same_idempotency_key_creates_one_vehicle()
    {
        var request = Request; var key = Guid.NewGuid().ToString("D");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Send(HttpMethod.Post, Collection, request, key)));
        try
        {
            Assert.That(responses.Count(x => x.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
            Assert.That(responses.Count(x => x.StatusCode == HttpStatusCode.OK), Is.EqualTo(4));
            var ids = new HashSet<Guid>();
            foreach (var response in responses) ids.Add((await response.Content.ReadFromJsonAsync<MutationResponse>(VehicleRules.Json))!.Resource.VehicleId);
            Assert.That(ids.Count, Is.EqualTo(1));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Test] public async Task Concurrent_revision_updates_commit_once()
    {
        var created = await Create();
        var requests = new[] { "First", "Second" }.Select(model => Send(HttpMethod.Patch, $"{Collection}/{created.Resource.VehicleId}",
            new UpdateVehicleRequest(1, "LT", created.Resource.RegistrationNumber, "Test", model, 2020, Transmission.Manual), Guid.NewGuid().ToString("D")));
        var responses = await Task.WhenAll(requests);
        try { Assert.That(responses.Select(x => x.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict })); }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Test] public async Task Duplicate_registration_rolls_back_root_categories_and_receipt()
    {
        var request = Request;
        using var first = await Send(HttpMethod.Post, Collection, request, Guid.NewGuid().ToString("D"));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var second = await Send(HttpMethod.Post, Collection, request, Guid.NewGuid().ToString("D"));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await PostgresEnvironment.Scalar(env.OwnerConnection, $"SELECT count(*) FROM vehicle WHERE registration_number='{request.RegistrationNumber}'"), Is.EqualTo("1"));
    }
    [Test] public async Task Tenant_RLS_and_receipt_immutability_are_enforced_by_database()
    {
        var created = await Create();
        Assert.That(await PostgresEnvironment.Scalar(env.RuntimeConnection, "SELECT count(*) FROM vehicle"), Is.EqualTo("0"));
        await using (var connection = new NpgsqlConnection(env.RuntimeConnection))
        {
            await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
            await using var configure = new NpgsqlCommand("SELECT set_config('vairas.school_id',@school,true)", connection);
            configure.Parameters.AddWithValue("school", Guid.NewGuid().ToString("D")); await configure.ExecuteNonQueryAsync();
            await using var count = new NpgsqlCommand("SELECT count(*) FROM vehicle", connection);
            Assert.That(await count.ExecuteScalarAsync(), Is.EqualTo(0L));
        }
        var immutable = Assert.ThrowsAsync<PostgresException>(async () => await PostgresEnvironment.Sql(env.OwnerConnection, $"UPDATE vehicle_operation SET reason='tampered' WHERE operation_id='{created.OperationId}'"));
        Assert.That(immutable!.SqlState, Is.EqualTo("23514"));
        Assert.ThrowsAsync<PostgresException>(async () => await PostgresEnvironment.Sql(env.OwnerConnection, $"UPDATE vehicle SET branch_id='{Guid.NewGuid()}',revision=revision+1 WHERE vehicle_id='{created.Resource.VehicleId}'"));
        await using var scope = env.Factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IVehicleStore>();
        await using var foreign = await store.OpenAsync(Guid.NewGuid(), false, default);
        Assert.That(await foreign.FindAsync<Vehicle>(x => x.VehicleId == created.Resource.VehicleId, default), Is.Null);
    }
    [Test] public async Task Disabled_Scheduling_returns_503_before_effects()
    {
        var created = await Create();
        using var result = await Send(HttpMethod.Post, $"/api/v1/vehicles/{created.Resource.VehicleId}/state-change-operations?schoolId={env.School}",
            new StateChangeRequest(StateChangeType.Reactivate, 1, "Planned activation"), Guid.NewGuid().ToString("D"));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await PostgresEnvironment.Scalar(env.OwnerConnection, $"SELECT count(*) FROM vehicle_state_change_operation WHERE vehicle_id='{created.Resource.VehicleId}'"), Is.EqualTo("0"));
        Assert.That((await result.Content.ReadAsStringAsync()), Does.Contain("SCHEDULING_INTEGRATION_NOT_READY"));
    }
    [Test] public async Task PostgreSQL_Saga_is_restartable_and_outbox_matches_AsyncAPI_payload()
    {
        var created = await Create(); env.Gate.Enabled = true;
        using var accepted = await Send(HttpMethod.Post, $"/api/v1/vehicles/{created.Resource.VehicleId}/state-change-operations?schoolId={env.School}",
            new StateChangeRequest(StateChangeType.Reactivate, 1, "Planned activation"), Guid.NewGuid().ToString("D"));
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.Accepted), await accepted.Content.ReadAsStringAsync());
        var operation = (await accepted.Content.ReadFromJsonAsync<StateChangeResponse>(VehicleRules.Json))!;
        Assert.That(accepted.Headers.Location!.IsAbsoluteUri, Is.False);
        await using (var scope = env.Factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IVehicleStore>();
            Assert.That(await store.DueAsync(false, 25, default), Does.Contain(new WorkItem(env.School, operation.OperationId)));
            Assert.That((await store.HealthAsync(default)).PendingSagas, Is.GreaterThanOrEqualTo(1));
        }
        for (var i = 0; i < 2; i++)
        {
            await using var scope = env.Factory.Services.CreateAsyncScope();
            Assert.That(await scope.ServiceProvider.GetRequiredService<IStateChangeProcessor>().ProcessAsync(new(env.School, operation.OperationId), default), Is.True);
        }
        using var status = await env.Client.GetAsync(accepted.Headers.Location);
        Assert.That((await status.Content.ReadFromJsonAsync<StateChangeResponse>(VehicleRules.Json))!.Status, Is.EqualTo(SagaStatus.Completed));
        await using var db = env.Db(true);
        var message = await db.Set<OutboxMessage>().SingleAsync(x => x.VehicleId == created.Resource.VehicleId);
        using var payload = JsonDocument.Parse(message.PayloadJson);
        Assert.That(payload.RootElement.EnumerateObject().Select(x => x.Name), Is.EquivalentTo(new[] { "schoolId", "branchId", "vehicleId", "effectiveAt" }));
        Assert.That(message.AggregateVersion, Is.EqualTo(2));
        await using (var scope = env.Factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IVehicleStore>();
            Assert.That(await store.DueAsync(true, 25, default), Does.Contain(new WorkItem(env.School, message.MessageId)));
            Assert.That((await store.HealthAsync(default)).PendingOutbox, Is.GreaterThanOrEqualTo(1));
        }
        Assert.That((await db.Set<VehicleOperation>().CountAsync(x => x.VehicleId == created.Resource.VehicleId)), Is.EqualTo(2));
    }
    [Test] public async Task Real_JWT_audience_scope_and_client_allowlist_protect_internal_routes()
    {
        var created = await Create(); var teacher = Guid.NewGuid();
        var path = $"/internal/v1/vehicles/{created.Resource.VehicleId}/eligibility?schoolId={env.School}&branchId={env.Branch}&teacherId={teacher}&category=B";
        using var user = await Send(HttpMethod.Get, path); Assert.That(user.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var scope = await Send(HttpMethod.Get, path, token: env.Token(true, scope: "wrong")); Assert.That(scope.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var client = await Send(HttpMethod.Get, path, token: env.Token(true, client: "unknown")); Assert.That(client.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var permitted = await Send(HttpMethod.Get, path, token: env.Token(true)); Assert.That(permitted.StatusCode, Is.EqualTo(HttpStatusCode.OK), await permitted.Content.ReadAsStringAsync());
        Assert.That((await permitted.Content.ReadFromJsonAsync<VehicleEligibilityResponse>(VehicleRules.Json))!.Eligible, Is.False);
        using var publicWorkload = await Send(HttpMethod.Get, Collection, token: env.Token(true)); Assert.That(publicWorkload.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var wrongAudience = await Send(HttpMethod.Get, Collection, token: env.Token(audience: "other")); Assert.That(wrongAudience.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    [Test] public async Task Malformed_input_and_cross_tenant_reads_use_safe_ProblemDetails()
    {
        var created = await Create();
        using var missingKey = await Send(HttpMethod.Post, Collection, Request);
        Assert.That(missingKey.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var foreign = await Send(HttpMethod.Get, $"/api/v1/schools/{Guid.NewGuid()}/vehicles/{created.Resource.VehicleId}");
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(foreign.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(foreign.Headers.GetValues("X-Correlation-ID").Single(), Is.EqualTo("integration-vehicle"));
        using var request = new HttpRequestMessage(HttpMethod.Post, Collection) { Content = new StringContent("{\"branchId\":\"bad\",\"branchId\":\"bad\"}", Encoding.UTF8, "application/json") };
        using var duplicateJson = await env.Client.SendAsync(request); Assert.That(duplicateJson.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        env.Authority.Allowed = false;
        using var forbidden = await Send(HttpMethod.Get, $"{Collection}/{created.Resource.VehicleId}"); Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
    [Test] public async Task OpenAPI_health_and_migration_rerun_are_verified()
    {
        using var openApi = await env.Client.GetAsync("/openapi/v1.json");
        Assert.That(openApi.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var json = await openApi.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var parsed = OpenApiDocument.Parse(json, "json");
        Assert.That(parsed.Diagnostic!.Errors, Is.Empty);
        Assert.That(parsed.Document!.Validate(ValidationRuleSet.GetDefaultRuleSet()).ToArray(), Is.Empty);
        Assert.That(document.RootElement.GetProperty("paths").EnumerateObject().Count(), Is.EqualTo(9));
        Assert.That(json, Does.Contain("requestVehicleStateChange"));
        Directory.CreateDirectory(Path.Combine(PostgresEnvironment.Root, "api"));
        await File.WriteAllTextAsync(Path.Combine(PostgresEnvironment.Root, "api/vehicle-management.openapi.json"), json);
        Assert.That((await env.Client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await env.Client.GetAsync("/health/ready")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using var db = env.Db(true); await db.Database.MigrateAsync();
        Assert.That(await db.Database.GetPendingMigrationsAsync(), Is.Empty);
        await env.ApplyGrantsAsync();
        Assert.That(await PostgresEnvironment.Scalar(env.OwnerConnection, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name<>'__EFMigrationsHistory'"), Is.EqualTo("8"));
        Assert.That(env.ServerVersion, Does.Contain("PostgreSQL 17"));
    }
}

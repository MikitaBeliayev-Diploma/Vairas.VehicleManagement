using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Middleware;
using Vairas.VehicleManagement.Persistance;

namespace Vairas.VehicleManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class ExceptionHandlingTests
{
    private static PostgresException Postgres(string sqlState, string? constraint = null) =>
        new("synthetic", "ERROR", "ERROR", sqlState, constraintName: constraint);

    private static DbUpdateException Update(string sqlState, string? constraint = null) => new("synthetic", Postgres(sqlState, constraint));

    [Test]
    public void Optimistic_concurrency_becomes_a_revision_conflict()
    {
        var translated = DatabaseExceptionTranslator.Translate(new DbUpdateConcurrencyException());
        Assert.That(translated, Is.TypeOf<ConflictException>());
        Assert.That(((BusinessException)translated!).Code, Is.EqualTo("REVISION_CONFLICT"));
    }

    [TestCase(PostgresErrorCodes.UniqueViolation, "vehicle_school_id_registration_country_code_registration_nu_idx")]
    
    public void Demonstrated_constraint_conflicts_keep_the_original(string state, string constraint)
    {
        var original = Update(state, constraint);
        var translated = DatabaseExceptionTranslator.Translate(original);
        Assert.That(translated, Is.TypeOf<ConflictException>());
        Assert.That(((BusinessException)translated!).Code, Is.EqualTo("RELATIONSHIP_CONFLICT"));
        Assert.That(translated!.InnerException, Is.SameAs(original));
    }

    [TestCase(PostgresErrorCodes.SerializationFailure)]
    [TestCase(PostgresErrorCodes.DeadlockDetected)]
    [TestCase(PostgresErrorCodes.LockNotAvailable)]
    public void Transient_transaction_failures_ask_for_a_retry(string state)
    {
        var translated = DatabaseExceptionTranslator.Translate(Postgres(state));
        Assert.That(translated, Is.TypeOf<ServiceUnavailableException>());
        Assert.That(((BusinessException)translated!).Code, Is.EqualTo("RETRY_REQUIRED"));
    }

    [TestCase(PostgresErrorCodes.UniqueViolation)]
    [TestCase(PostgresErrorCodes.ExclusionViolation)]
    [TestCase(PostgresErrorCodes.ForeignKeyViolation)]
    [TestCase(PostgresErrorCodes.CheckViolation)]
    public void Unrecognised_integrity_violations_are_implementation_defects_not_business_conflicts(string state)
    {
        var translated = DatabaseExceptionTranslator.Translate(Update(state, "operation_resolution_time"));
        Assert.That(translated, Is.TypeOf<PersistenceFailureException>());
        var failure = (PersistenceFailureException)translated!;
        Assert.That((failure.SqlState, failure.Constraint), Is.EqualTo((state, "operation_resolution_time")));
        Assert.That(translated, Is.Not.InstanceOf<BusinessException>());
    }

    [Test]
    public void Business_exceptions_and_cancellation_pass_through_untranslated()
    {
        Assert.That(DatabaseExceptionTranslator.Translate(new NotFoundException("NOT_FOUND")), Is.Null);
        Assert.That(DatabaseExceptionTranslator.Translate(new OperationCanceledException()), Is.Null);
        Assert.That(DatabaseExceptionTranslator.Translate(new InvalidOperationException()), Is.Null);
    }

    private static async Task<(int Status, string Body)> Handle(Exception exception, CancellationToken aborted = default)
    {
        var http = new DefaultHttpContext { RequestAborted = aborted };
        http.Response.Body = new MemoryStream();
        await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(http, exception, default);
        http.Response.Body.Position = 0;
        return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync());
    }

    [TestCaseSource(nameof(Categories))]
    public async Task Each_application_category_maps_to_its_http_status_with_the_existing_code((BusinessException Exception, int Status) item)
    {
        var (status, body) = await Handle(item.Exception);
        Assert.That(status, Is.EqualTo(item.Status));
        Assert.That(body, Does.Contain("\"code\":\"" + item.Exception.Code + "\""));
    }

    private static IEnumerable<(BusinessException, int)> Categories() =>
    [
        (new BadRequestException("VALIDATION_FAILED"), 400), (new UnauthorizedException("SESSION_INVALID"), 401), (new ForbiddenException("ACCESS_DENIED"), 403),
        (new NotFoundException("NOT_FOUND"), 404), (new ConflictException("REVISION_CONFLICT"), 409), (new UnprocessableEntityException("ASSESSMENT_NOT_READY"), 422),
        (new ServiceUnavailableException("DEPENDENCY_UNAVAILABLE"), 503)
    ];

    [Test]
    public async Task Unexpected_failures_are_a_generic_500_without_internal_details()
    {
        var (status, body) = await Handle(new PersistenceFailureException("23514", "operation_resolution_time", new InvalidOperationException("secret-connection-detail")));
        Assert.That(status, Is.EqualTo(500));
        Assert.That(body, Does.Contain("INTERNAL_ERROR"));
        Assert.That(body, Does.Not.Contain("secret-connection-detail").And.Not.Contain("operation_resolution_time").And.Not.Contain("23514"));
    }

    [Test]
    public async Task A_canceled_request_is_not_reported_as_a_server_error()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var (status, body) = await Handle(new OperationCanceledException(), source.Token);
        Assert.That(body, Is.Empty);
        Assert.That(status, Is.Not.EqualTo(500));
    }
}

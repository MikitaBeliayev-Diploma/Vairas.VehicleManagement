using NUnit.Framework;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class VehicleBusinessTests
{
    private FixedClock clock = null!; private MemoryStore store = null!; private TestAuthority authority = null!;
    private TestUsers users = null!; private TestGate gate = null!; private VehicleService service = null!; private StateChangeProcessor processor = null!;
    private Guid school, branch, admin, teacher;
    private VehicleActor Actor => new(admin, clock.Now.AddMinutes(1));
    private CreateVehicleRequest CreateRequest => new(branch, "lt", " test 123 ", "Test", "Model", 2020, Transmission.Manual, ["b"]);
    private Task<MutationResponse> Create(string key = "create-key-01") => service.CreateAsync(school, key, CreateRequest, Actor, default);
    private Vehicle Vehicle(Guid id) => store.Rows.OfType<Vehicle>().Single(x => x.VehicleId == id);
    private StateChangeOperation Operation(Guid id) => store.Rows.OfType<StateChangeOperation>().Single(x => x.OperationId == id);
    [SetUp] public void Setup()
    {
        clock = new(); store = new(clock); authority = new(clock); users = new(); gate = new(); school = Guid.NewGuid(); branch = Guid.NewGuid(); admin = Guid.NewGuid(); teacher = Guid.NewGuid();
        service = new(store, authority, users, new TestCatalogue(), gate, clock); processor = new(store, gate, authority, users, new TestCatalogue(), clock);
    }
    private async Task<StateChangeResponse> Change(Guid id, StateChangeType type, string key = "state-key-01") =>
        await service.RequestStateAsync(school, id, key, new(type, Vehicle(id).Revision, "Planned test change"), Actor, "unit-correlation", default);
    private async Task Finish(Guid operation)
    {
        for (var i = 0; i < 8 && !VehicleRules.Terminal(Operation(operation).Status); i++)
        { clock.Now = clock.Now.AddSeconds(310); await processor.ProcessAsync(new(school, operation), default); }
    }
    [Test] public async Task Create_is_atomic_normalized_and_replays_the_original_result()
    {
        var first = await Create();
        Assert.That(first.Resource, Has.Property("Status").EqualTo(VehicleStatus.Inactive));
        Assert.That(first.Resource.Categories, Is.EqualTo(new[] { "B" }));
        Assert.That(first.Resource.RegistrationNumber, Is.EqualTo("TEST 123"));
        Vehicle(first.Resource.VehicleId).Model = "Later model";
        var replay = await Create();
        Assert.That(replay.Replayed, Is.True); Assert.That(replay.Resource.Model, Is.EqualTo("Model"));
        Assert.That(store.Rows.OfType<VehicleOperation>().Count(), Is.EqualTo(1));
        Assert.That((await service.GetAsync(school, first.Resource.VehicleId, Actor, default)).Model, Is.EqualTo("Later model"));
    }
    [Test] public async Task Changed_idempotent_intent_conflicts_and_unauthorized_replay_is_denied()
    {
        await Create();
        Assert.That(Business.ThrowsBusinessAsync(async () => await service.CreateAsync(school, "create-key-01", CreateRequest with { Model = "Different" }, Actor, default))!.Code, Is.EqualTo("IDEMPOTENCY_MISMATCH"));
        authority.Allowed = false;
        Assert.That(Business.ThrowsBusinessAsync(async () => await Create()).HttpStatus(), Is.EqualTo(403));
    }
    [Test] public async Task Metadata_update_is_revision_checked_and_replay_preserves_result()
    {
        var id = (await Create()).Resource.VehicleId;
        var update = new UpdateVehicleRequest(1, "LT", "NEXT123", "Factory", "Next", 2021, Transmission.Automatic);
        var result = await service.UpdateAsync(school, id, "update-key-01", update, Actor, default);
        Assert.That(result.Resource.Revision, Is.EqualTo(2));
        Assert.That((await service.UpdateAsync(school, id, "update-key-01", update, Actor, default)).Replayed, Is.True);
        Assert.That(Business.ThrowsBusinessAsync(async () => await service.UpdateAsync(school, id, "update-key-02", update, Actor, default)).HttpStatus(), Is.EqualTo(409));
    }
    [Test] public async Task Category_replacement_preserves_nonempty_set_and_has_exact_replay()
    {
        var id = (await Create()).Resource.VehicleId;
        var request = new ReplaceCategoriesRequest(1, ["be"]);
        var result = await service.CategoriesAsync(school, id, "category-key-01", request, Actor, default);
        Assert.That(result.Resource.Categories, Is.EqualTo(new[] { "BE" }));
        Assert.That((await service.CategoriesAsync(school, id, "category-key-01", request, Actor, default)).Replayed, Is.True);
        Business.ThrowsBusinessAsync(async () => await service.CategoriesAsync(school, id, "category-key-02", new(2, []), Actor, default));
    }
    [Test] public async Task Assignments_bind_exact_membership_and_require_explicit_reassignment_after_rejoin()
    {
        var id = (await Create()).Resource.VehicleId;
        var initialMembership = Guid.NewGuid(); authority.Memberships[teacher] = initialMembership;
        var assigned = await service.TeachersAsync(school, id, "teacher-key-01", new(1, [teacher]), Actor, default);
        Assert.That(assigned.Resource.TeacherAssignments.Single().SchoolMembershipId, Is.EqualTo(initialMembership));
        Assert.That((await service.TeachersAsync(school, id, "teacher-key-01", new(1, [teacher]), Actor, default)).Replayed, Is.True);
        var activation = await Change(id, StateChangeType.Reactivate); await Finish(activation.OperationId);
        Assert.That((await service.EligibilityAsync(school, id, branch, teacher, "B", default)).Eligible, Is.True);
        authority.Memberships[teacher] = Guid.NewGuid();
        Assert.That((await service.EligibilityAsync(school, id, branch, teacher, "B", default)).Eligible, Is.False);
    }
    [Test] public async Task Assignment_replacement_updates_memberships_and_removes_only_requested_links()
    {
        var id = (await Create()).Resource.VehicleId;
        await service.TeachersAsync(school, id, "teacher-key-01", new(1, [teacher]), Actor, default);
        var next = Guid.NewGuid(); authority.Memberships[teacher] = next;
        var result = await service.TeachersAsync(school, id, "teacher-key-02", new(2, [teacher]), Actor, default);
        Assert.That(result.Resource.TeacherAssignments.Single().SchoolMembershipId, Is.EqualTo(next));
        Assert.That((await service.TeachersAsync(school, id, "teacher-key-03", new(3, []), Actor, default)).Resource.TeacherAssignments, Is.Empty);
    }
    [Test] public async Task Assignment_rejects_inactive_or_misbound_teacher_without_changes()
    {
        var id = (await Create()).Resource.VehicleId; users.Inactive.Add(teacher);
        Business.ThrowsBusinessAsync(async () => await service.TeachersAsync(school, id, "teacher-key-01", new(1, [teacher]), Actor, default));
        users.Inactive.Clear(); users.BrokenBinding = true;
        Business.ThrowsBusinessAsync(async () => await service.TeachersAsync(school, id, "teacher-key-02", new(1, [teacher]), Actor, default));
        Assert.That(Vehicle(id).Revision, Is.EqualTo(1));
    }
    [Test] public async Task Eligibility_reports_status_category_branch_and_teacher_reasons()
    {
        var id = (await Create()).Resource.VehicleId;
        var result = await service.EligibilityAsync(school, id, Guid.NewGuid(), teacher, "BE", default);
        Assert.That(result.Reasons, Is.EquivalentTo(new[] { "STATUS_NOT_ACTIVE", "CATEGORY_NOT_SUPPORTED", "TEACHER_NOT_ASSIGNED", "BRANCH_MISMATCH" }));
        authority.Expired = true;
        Assert.That(Business.ThrowsBusinessAsync(async () => await service.EligibilityAsync(school, id, branch, teacher, "B", default)).HttpStatus(), Is.EqualTo(503));
    }
    [Test] public async Task Eligibility_does_not_admit_pending_status_changes_or_inactive_users()
    {
        var id = (await Create()).Resource.VehicleId;
        await service.TeachersAsync(school, id, "teacher-key-01", new(1, [teacher]), Actor, default);
        var activation = await Change(id, StateChangeType.Reactivate); await Finish(activation.OperationId);
        users.Inactive.Add(teacher);
        Assert.That((await service.EligibilityAsync(school, id, branch, teacher, "B", default)).Eligible, Is.False);
        users.Inactive.Clear(); await Change(id, StateChangeType.Deactivate, "deactivate-key");
        Assert.That((await service.EligibilityAsync(school, id, branch, teacher, "B", default)).Eligible, Is.False);
    }
    [Test] public async Task Pagination_is_bounded_and_tenant_scoped()
    {
        await Create("create-key-01"); await Create("create-key-02");
        var first = await service.ListAsync(school, null, 1, Actor, default);
        Assert.That(first.NextCursor, Is.Not.Null);
        var second = await service.ListAsync(school, first.NextCursor, 1, Actor, default);
        Assert.That(second.Items.Single().VehicleId, Is.Not.EqualTo(first.Items.Single().VehicleId)); Assert.That(second.NextCursor, Is.Null);
        Assert.That((await service.ListAsync(Guid.NewGuid(), null, 50, Actor, default)).Items, Is.Empty);
        Business.ThrowsBusinessAsync(async () => await service.ListAsync(school, null, 0, Actor, default));
        Business.ThrowsBusinessAsync(async () => await service.GetAsync(school, Guid.NewGuid(), Actor, default));
    }
    [Test] public async Task Missing_Scheduling_never_creates_a_fake_accepted_operation()
    {
        var id = (await Create()).Resource.VehicleId; gate.Enabled = false;
        var before = store.Commits;
        Assert.That(Business.ThrowsBusinessAsync(async () => await Change(id, StateChangeType.Reactivate))!.Code, Is.EqualTo("SCHEDULING_INTEGRATION_NOT_READY"));
        Assert.That(store.Commits, Is.EqualTo(before)); Assert.That(store.Rows.OfType<StateChangeOperation>(), Is.Empty);
        Assert.That(await processor.ProcessAsync(new(school, Guid.NewGuid()), default), Is.False);
    }
    [Test] public async Task An_imported_inactive_row_is_not_assumed_to_have_never_been_bookable()
    {
        var id = Guid.NewGuid();
        store.Rows.Add(new Vehicle { SchoolId = school, VehicleId = id, BranchId = branch, Status = VehicleStatus.Inactive, Revision = 1 });
        store.Rows.Add(new VehicleCategory { SchoolId = school, VehicleId = id, CategoryCode = "B" });
        Assert.That(Business.ThrowsBusinessAsync(async () => await service.CategoriesAsync(school, id, "import-edit-key", new(1, ["BE"]), Actor, default))!.Code, Is.EqualTo("VEHICLE_ORIGIN_NOT_VERIFIED"));
        Assert.That(Business.ThrowsBusinessAsync(async () => await Change(id, StateChangeType.Reactivate))!.Code, Is.EqualTo("VEHICLE_ORIGIN_NOT_VERIFIED"));
        Assert.That(store.Rows.OfType<StateChangeOperation>(), Is.Empty);
    }
    [Test] public async Task Saga_replay_returns_same_operation_and_pending_prevents_edits()
    {
        var id = (await Create()).Resource.VehicleId;
        var request = new StateChangeRequest(StateChangeType.Reactivate, 1, "Planned test change");
        var first = await Change(id, StateChangeType.Reactivate);
        Assert.That((await service.RequestStateAsync(school, id, "state-key-01", request, Actor, "other-corr", default)).OperationId, Is.EqualTo(first.OperationId));
        Assert.That((await service.GetStateAsync(school, id, first.OperationId, Actor, default)).Status, Is.EqualTo(SagaStatus.Pending));
        Business.ThrowsBusinessAsync(async () => await service.CategoriesAsync(school, id, "categories-key", new(1, ["BE"]), Actor, default));
        Business.ThrowsBusinessAsync(async () => await service.UpdateAsync(school, id, "metadata-key", new(1, "LT", "TEST123", "Test", "Model", 2020, Transmission.Manual), Actor, default));
        Business.ThrowsBusinessAsync(async () => await Change(id, StateChangeType.Reactivate, "second-state-key"));
        await Finish(first.OperationId);
        Assert.That((await service.RequestStateAsync(school, id, "state-key-01", request, Actor, "corr", default)).Status, Is.EqualTo(SagaStatus.Completed));
    }
    [Test] public async Task Block_first_deactivation_and_active_first_reactivation_are_durable()
    {
        var id = (await Create()).Resource.VehicleId;
        var first = await Change(id, StateChangeType.Reactivate); await Finish(first.OperationId);
        Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Active));
        Business.ThrowsBusinessAsync(async () => await service.TeachersAsync(school, id, "teacher-key-01", new(2, [teacher]), Actor, default));
        var stop = await Change(id, StateChangeType.Deactivate, "deactivate-key");
        await processor.ProcessAsync(new(school, stop.OperationId), default);
        await processor.ProcessAsync(new(school, stop.OperationId), default);
        Assert.That(Operation(stop.OperationId).Status, Is.EqualTo(SagaStatus.SchedulingBlocked)); Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Active));
        await Finish(stop.OperationId); Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Inactive));
        await service.CategoriesAsync(school, id, "category-key-02", new(Vehicle(id).Revision, ["BE"]), Actor, default);
        var restart = await Change(id, StateChangeType.Reactivate, "reactivate-key");
        await processor.ProcessAsync(new(school, restart.OperationId), default);
        await processor.ProcessAsync(new(school, restart.OperationId), default);
        Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Active)); Assert.That(gate.Releases, Is.Zero);
        Assert.That(Operation(restart.OperationId).Status, Is.EqualTo(SagaStatus.ReleasingBlock));
        await Finish(restart.OperationId); Assert.That(gate.Releases, Is.EqualTo(1));
        Assert.That(store.Rows.OfType<OutboxMessage>().Count(), Is.EqualTo(3));
    }
    [Test] public async Task Deterministic_booking_conflict_rejects_before_vehicle_change()
    {
        var id = (await Create()).Resource.VehicleId; var start = await Change(id, StateChangeType.Reactivate); await Finish(start.OperationId);
        gate.Reject = true; var stop = await Change(id, StateChangeType.Deactivate, "deactivate-key"); await Finish(stop.OperationId);
        Assert.That(Operation(stop.OperationId).Status, Is.EqualTo(SagaStatus.Rejected)); Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Active));
    }
    [Test] public async Task Timeout_after_remote_commit_recovers_the_existing_block_without_duplication()
    {
        var id = (await Create()).Resource.VehicleId; var start = await Change(id, StateChangeType.Reactivate); await Finish(start.OperationId);
        gate.ThrowAfterBlock = true; var stop = await Change(id, StateChangeType.Deactivate, "deactivate-key");
        await processor.ProcessAsync(new(school, stop.OperationId), default); await processor.ProcessAsync(new(school, stop.OperationId), default);
        Assert.That(Operation(stop.OperationId).Status, Is.EqualTo(SagaStatus.NeedsRecovery)); Assert.That(gate.Releases, Is.Zero);
        // A new processor instance represents a restarted worker.
        processor = new(store, gate, authority, users, new TestCatalogue(), clock); await Finish(stop.OperationId);
        Assert.That(Operation(stop.OperationId).Status, Is.EqualTo(SagaStatus.Completed)); Assert.That(gate.Blocks, Is.EqualTo(1));
        Assert.That(await processor.ProcessAsync(new(school, stop.OperationId), default), Is.False);
    }
    [Test] public async Task Uncertain_release_keeps_active_vehicle_blocked_and_recovers()
    {
        var id = (await Create()).Resource.VehicleId; var start = await Change(id, StateChangeType.Reactivate); await Finish(start.OperationId);
        var stop = await Change(id, StateChangeType.Deactivate, "deactivate-key"); await Finish(stop.OperationId);
        var restart = await Change(id, StateChangeType.Reactivate, "reactivate-key");
        await processor.ProcessAsync(new(school, restart.OperationId), default); await processor.ProcessAsync(new(school, restart.OperationId), default);
        gate.Unavailable = true; await processor.ProcessAsync(new(school, restart.OperationId), default);
        Assert.That(Operation(restart.OperationId).Status, Is.EqualTo(SagaStatus.NeedsRecovery)); Assert.That(Vehicle(id).Status, Is.EqualTo(VehicleStatus.Active));
        gate.Unavailable = false; await Finish(restart.OperationId); Assert.That(Operation(restart.OperationId).Status, Is.EqualTo(SagaStatus.Completed));
    }
    [Test] public async Task Invalid_transitions_and_stale_or_foreign_authority_are_rejected()
    {
        var id = (await Create()).Resource.VehicleId;
        Assert.That(Business.ThrowsBusinessAsync(async () => await Change(id, StateChangeType.Deactivate)).HttpStatus(), Is.EqualTo(422));
        authority.Alter = (_, response) => response with { SchoolId = Guid.NewGuid() };
        Assert.That(Business.ThrowsBusinessAsync(async () => await service.GetAsync(school, id, Actor, default)).HttpStatus(), Is.EqualTo(503));
        authority.Alter = null;
        Business.ThrowsBusinessAsync(async () => await service.GetAsync(school, id, new(admin, clock.Now), default));
    }
    [Test] public async Task Expired_or_taken_worker_lease_is_not_executed()
    {
        var id = (await Create()).Resource.VehicleId; var operation = await Change(id, StateChangeType.Reactivate);
        Operation(operation.OperationId).LeaseToken = Guid.NewGuid(); Operation(operation.OperationId).LeaseUntil = clock.Now.AddMinutes(1);
        Assert.That(await processor.ProcessAsync(new(school, operation.OperationId), default), Is.False);
        Assert.That(await processor.ProcessAsync(new(school, Guid.NewGuid()), default), Is.False);
        clock.Now = clock.Now.AddMinutes(2); await Finish(operation.OperationId);
        Assert.That(Operation(operation.OperationId).Status, Is.EqualTo(SagaStatus.Completed));
    }
    [Test] public async Task Outbox_retries_stable_message_identity_and_never_republishes_a_delivered_row()
    {
        var id = (await Create()).Resource.VehicleId; var start = await Change(id, StateChangeType.Reactivate); await Finish(start.OperationId);
        var message = store.Rows.OfType<OutboxMessage>().Single(); var item = new WorkItem(school, message.MessageId); var publisher = new TestPublisher { Fail = true };
        var dispatcher = new OutboxDispatcher(store, publisher); await dispatcher.DispatchAsync(item, default);
        Assert.That(store.Rows.OfType<OutboxMessage>().Single().PublishedAt, Is.Null);
        Assert.That(await dispatcher.DispatchAsync(item, default), Is.False);
        clock.Now = clock.Now.AddMinutes(10); publisher.Fail = false; await dispatcher.DispatchAsync(item, default);
        Assert.That(publisher.Sent, Is.EqualTo(new[] { message.MessageId }));
        Assert.That(await dispatcher.DispatchAsync(item, default), Is.False);
        Assert.That(await dispatcher.DispatchAsync(new(school, Guid.NewGuid()), default), Is.False);
        Assert.That(await store.DueAsync(true, 25, default), Is.Empty);
    }
    [TestCase("")][TestCase("short")][TestCase("bad key with spaces")]
    public void Invalid_keys_fail_validation(string key) => Business.ThrowsBusiness(() => VehicleRules.Key(key));
    [TestCase(1885)][TestCase(2030)] public void Year_is_a_technical_validation_not_an_invented_legal_rule(short year) =>
        Business.ThrowsBusiness(() => VehicleRules.Details(year, Transmission.Manual, clock.Now));
    [Test] public void Collections_and_identifiers_reject_ambiguous_inputs()
    {
        Business.ThrowsBusiness(() => VehicleRules.Id(Guid.Empty));
        Business.ThrowsBusiness(() => VehicleRules.Country("123"));
        Business.ThrowsBusiness(() => VehicleRules.Categories(["B", "b"], new TestCatalogue()));
        Business.ThrowsBusiness(() => VehicleRules.Categories(["UNKNOWN"], new TestCatalogue()));
        Business.ThrowsBusiness(() => VehicleRules.Text("\n", 100));
        Business.ThrowsBusinessAsync(async () => await service.TeachersAsync(school, Guid.NewGuid(), "teacher-key-01", new(1, [teacher, teacher]), Actor, default));
    }
}

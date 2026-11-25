using System.Net;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Integrations;
public sealed class SchedulingGate(IDependencyTransport transport, IOptions<DependencyOptions> dependenciesSettings, IOptions<SchedulingOptions> optionsSettings) : ISchedulingGate
{
    private readonly SchedulingOptions options = optionsSettings.Value;
    private readonly DependencyOptions dependencies = dependenciesSettings.Value;
    public bool Enabled => options.Enabled;

    public async Task<BlockResult?> OutcomeAsync(Guid operationId, CancellationToken ct)
    {
        RequireEnabled();
        using var response = await transport.RequestAsync(dependencies.SchedulingBaseUrl, $"internal/v1/vehicle-blocks/by-operation/{operationId:D}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        return await Read(response, ct);
    }

    public async Task<BlockResult> BlockAsync(Guid operationId, Guid vehicleId, CancellationToken ct)
    {
        RequireEnabled();
        using var response = await transport.RequestAsync(dependencies.SchedulingBaseUrl, "internal/v1/vehicle-blocks", new { operationId, vehicleId, reason = "Deactivation", conflictPolicy = "RejectIfFutureBookings" }, ct);
        // A 409 is only terminal after the owning service exposes its durable outcome.
        if (response.StatusCode == HttpStatusCode.Conflict)
            return await OutcomeAsync(operationId, ct) ?? throw VehicleErrors.Dependency("BLOCK_OUTCOME_UNKNOWN");
        return await Read(response, ct);
    }

    public async Task<BlockResult> ReleaseAsync(Guid blockId, Guid blockOperationId, Guid releaseOperationId, CancellationToken ct)
    {
        RequireEnabled();
        using var response = await transport.RequestAsync(dependencies.SchedulingBaseUrl, $"internal/v1/vehicle-blocks/{blockId:D}/release", new { operationId = blockOperationId, releaseOperationId, reason = "Reactivated" }, ct);
        return await Read(response, ct);
    }

    public async Task<BlockResult?> OutcomeAsync(Guid school,Guid operation,CancellationToken ct)
    {
        RequireEnabled();
        using var response=await transport.RequestAsync(dependencies.SchedulingBaseUrl,$"internal/v1/vehicle-blocks/by-operation/{operation:D}?schoolId={school:D}",null,ct);
        return response.StatusCode==HttpStatusCode.NotFound?null:await Read(response,ct);
    }
    public async Task<BlockResult> BlockAsync(Guid school,Guid operation,Guid vehicle,CancellationToken ct)
    {
        RequireEnabled();
        using var response=await transport.RequestAsync(dependencies.SchedulingBaseUrl,"internal/v1/vehicle-blocks",new {schoolId=school,operationId=operation,vehicleId=vehicle,reason="Deactivation",conflictPolicy="RejectIfFutureBookings"},ct);
        if(response.StatusCode==HttpStatusCode.Conflict)return await OutcomeAsync(school,operation,ct)??throw VehicleErrors.Dependency("BLOCK_OUTCOME_UNKNOWN");
        return await Read(response,ct);
    }
    public async Task<BlockResult> ReleaseAsync(Guid school,Guid block,Guid owningOperation,Guid releaseOperation,CancellationToken ct)
    {
        RequireEnabled();
        using var response=await transport.RequestAsync(dependencies.SchedulingBaseUrl,$"internal/v1/vehicle-blocks/{block:D}/release",new {schoolId=school,operationId=owningOperation,releaseOperationId=releaseOperation,reason="Reactivated"},ct);
        var result=await Read(response,ct);
        if(result.OperationId!=owningOperation || result.BlockId!=block)throw VehicleErrors.Dependency("BLOCK_RELEASE_UNCERTAIN");
        return result with {OperationId=releaseOperation};
    }
    private void RequireEnabled()
    {
        if (!Enabled)
            throw VehicleErrors.Dependency("SCHEDULING_INTEGRATION_NOT_READY");
    }

    private static async Task<BlockResult> Read(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw VehicleErrors.Dependency();
        try
        {
            return await response.Content.ReadFromJsonAsync<BlockResult>(VehicleRules.Json, ct) ?? throw VehicleErrors.Dependency();
        }
        catch (JsonException)
        {
            throw VehicleErrors.Dependency();
        }
    }
}

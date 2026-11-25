using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Integrations;
using Vairas.VehicleManagement.Middleware;

namespace Vairas.VehicleManagement.Controllers;
[Authorize(Policy = "VehicleEntry"), Route("api/v1/vehicles/{vehicleId:guid}/state-change-operations")]
public sealed class VehicleOperationsController(IVehicleService service, IVehicleActorFactory actors) : BaseApiController
{
    /// <summary>Persist a planned deactivation or activation Saga; accepted does not mean completed.</summary>
    [HttpPost(Name = "requestVehicleStateChange"), ProducesResponseType<StateChangeResponse>(202), ProducesResponseType<StateChangeResponse>(200)]
    public async Task<ActionResult<StateChangeResponse>> RequestChange(Guid vehicleId, [FromQuery] Guid schoolId, StateChangeRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await service.RequestStateAsync(schoolId, vehicleId, key, request, await actors.CreateAsync(HttpContext, ct), CorrelationMiddleware.Id(HttpContext), ct);
        if (VehicleRules.Terminal(result.Status))
            return Ok(result);
        Response.Headers.RetryAfter = "2";
        return Accepted($"/api/v1/vehicles/{vehicleId:D}/state-change-operations/{result.OperationId:D}?schoolId={schoolId:D}", result);
    }

    /// <summary>Read the authoritative persisted state after acceptance or an uncertain client timeout.</summary>
    [HttpGet("{operationId:guid}", Name = "getVehicleStateChangeOperation")]
    public async Task<ActionResult<StateChangeResponse>> Get(Guid vehicleId, Guid operationId, [FromQuery] Guid schoolId, CancellationToken ct) => Ok(await service.GetStateAsync(schoolId, vehicleId, operationId, await actors.CreateAsync(HttpContext, ct), ct));
}

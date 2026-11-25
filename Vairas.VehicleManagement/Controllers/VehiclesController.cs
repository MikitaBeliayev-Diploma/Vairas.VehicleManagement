using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Integrations;

namespace Vairas.VehicleManagement.Controllers;

[Authorize(Policy = "VehicleEntry"), Route("api/v1/schools/{schoolId:guid}/vehicles")]
public sealed class VehiclesController(IVehicleService service, IVehicleActorFactory actors) : BaseApiController
{
    /// <summary>List vehicles in the administrator's current school scope.</summary>
    [HttpGet(Name = "listVehicles")]
    public async Task<ActionResult<VehicleListResponse>> List(Guid schoolId, Guid? after, CancellationToken ct, int limit = 50) =>
        Ok(await service.ListAsync(schoolId, after, limit, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Read one vehicle under current school administrator authority.</summary>
    [HttpGet("{vehicleId:guid}", Name = "getVehicle")]
    public async Task<ActionResult<VehicleResponse>> Get(Guid schoolId, Guid vehicleId, CancellationToken ct) =>
        Ok(await service.GetAsync(schoolId, vehicleId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Create an inactive vehicle and its nonempty supported category collection atomically.</summary>
    [HttpPost(Name = "createVehicle"), ProducesResponseType<MutationResponse>(201), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Create(Guid schoolId, CreateVehicleRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await service.CreateAsync(schoolId, key, request, await actors.CreateAsync(HttpContext, ct), ct);
        return result.Replayed ? Ok(result) : Created($"/api/v1/schools/{schoolId:D}/vehicles/{result.Resource.VehicleId:D}", result);
    }
    /// <summary>Replace the descriptive fields with an explicit expected revision.</summary>
    [HttpPatch("{vehicleId:guid}", Name = "updateVehicle")]
    public async Task<ActionResult<MutationResponse>> Update(Guid schoolId, Guid vehicleId, UpdateVehicleRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Ok(await service.UpdateAsync(schoolId, vehicleId, key, request, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Replace categories before first activation or after confirmed Scheduling blocking.</summary>
    [HttpPut("{vehicleId:guid}/categories", Name = "replaceVehicleCategories")]
    public async Task<ActionResult<MutationResponse>> Categories(Guid schoolId, Guid vehicleId, ReplaceCategoriesRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Ok(await service.CategoriesAsync(schoolId, vehicleId, key, request, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Replace teacher assignments, binding each teacher to the exact current school membership.</summary>
    [HttpPut("{vehicleId:guid}/teacher-assignments", Name = "replaceVehicleTeacherAssignments")]
    public async Task<ActionResult<MutationResponse>> Teachers(Guid schoolId, Guid vehicleId, ReplaceTeacherAssignmentsRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Ok(await service.TeachersAsync(schoolId, vehicleId, key, request, await actors.CreateAsync(HttpContext, ct), ct));
}

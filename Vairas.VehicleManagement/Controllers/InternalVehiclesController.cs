using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Integrations;
using Vairas.VehicleManagement.Middleware;

namespace Vairas.VehicleManagement.Controllers;
[Route("internal/v1/vehicles")]
public sealed class InternalVehiclesController(IVehicleService service) : BaseApiController
{
    /// <summary>Return an exact resource snapshot and explicit durable gate capability to Scheduling.</summary>
    [HttpGet("{vehicleId:guid}/scheduling-eligibility",Name="getSchedulingVehicleEligibility"),Authorize(Policy="vehicles.eligibility.read")]
    public async Task<ActionResult<SchedulingVehicleSnapshot>> Scheduling(Guid vehicleId,Guid schoolId,Guid branchId,Guid teacherId,string category,CancellationToken ct)=>Ok(await service.SchedulingEligibilityAsync(schoolId,vehicleId,branchId,teacherId,category,ct));
    /// <summary>Verify tenant ownership before acquiring a durable Scheduling block.</summary>
    [HttpGet("{vehicleId:guid}/ownership",Name="getSchedulingVehicleOwnership"),Authorize(Policy="vehicles.eligibility.read")]
    public async Task<ActionResult<VehicleOwnershipSnapshot>> Ownership(Guid vehicleId,Guid schoolId,CancellationToken ct)=>Ok(await service.OwnershipAsync(schoolId,vehicleId,ct));
    /// <summary>Check current status, category, exact teacher authority and branch; this is not a booking.</summary>
    [HttpGet("{vehicleId:guid}/eligibility", Name = "getVehicleEligibility"), Authorize(Policy = "vehicles.eligibility.read")]
    public async Task<ActionResult<VehicleEligibilityResponse>> Eligibility(Guid vehicleId, Guid schoolId, Guid branchId, Guid teacherId, string category, CancellationToken ct) => Ok(await service.EligibilityAsync(schoolId, vehicleId, branchId, teacherId, category, ct));
}

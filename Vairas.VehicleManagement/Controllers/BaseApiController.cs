using Microsoft.AspNetCore.Mvc;

namespace Vairas.VehicleManagement.Controllers;

/// <summary>
/// Common HTTP-level contract for MVC controllers: automatic model validation (converted to the VALIDATION_FAILED
/// problem by ConfigureExceptionHandling) and binding-source inference. Authorization stays on each controller.
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase;

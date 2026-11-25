using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
/// <summary>Factories for the common application errors; each returns the category exception for the intended HTTP semantics.</summary>
public static class VehicleErrors
{
    public static BadRequestException Invalid() => new("VALIDATION_FAILED");
    public static NotFoundException Missing() => new("NOT_FOUND");
    public static ForbiddenException Denied() => new("ACCESS_DENIED");
    public static ServiceUnavailableException Dependency(string code = "DEPENDENCY_UNAVAILABLE") => new(code);
    public static ConflictException Conflict(string code = "REVISION_CONFLICT") => new(code);

    /// <summary>Rebuilds the category from a persisted outcome status (idempotent replay of a stored decision).</summary>
    public static BusinessException FromStatus(int status, string code) => status switch
    {
        400 => new BadRequestException(code), 401 => new UnauthorizedException(code), 403 => new ForbiddenException(code),
        404 => new NotFoundException(code), 409 => new ConflictException(code), 422 => new UnprocessableEntityException(code),
        503 => new ServiceUnavailableException(code),
        _ => throw new InvalidOperationException("A persisted outcome carries an unsupported status.")
    };
}

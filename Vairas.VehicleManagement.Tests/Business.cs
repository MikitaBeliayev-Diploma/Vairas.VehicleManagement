using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Tests;

/// <summary>Assertions on application errors by category; the HTTP status is derived exactly as the API layer maps it.</summary>
internal static class Business
{
    public static BusinessException ThrowsBusinessAsync(Func<Task> action) => NUnit.Framework.Assert.CatchAsync<BusinessException>(async () => await action())!;
    public static BusinessException ThrowsBusiness(Action action) => NUnit.Framework.Assert.Catch<BusinessException>(() => action())!;

    public static int HttpStatus(this BusinessException exception) => exception switch
    {
        BadRequestException => 400, UnauthorizedException => 401, ForbiddenException => 403, NotFoundException => 404,
        ConflictException => 409, UnprocessableEntityException => 422, ServiceUnavailableException => 503,
        _ => 500
    };
}

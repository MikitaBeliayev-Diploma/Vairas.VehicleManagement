namespace Vairas.VehicleManagement.Business;

/// <summary>The request is malformed or fails validation.</summary>
public sealed class BadRequestException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

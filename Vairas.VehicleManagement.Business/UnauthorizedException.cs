namespace Vairas.VehicleManagement.Business;

/// <summary>The caller is not authenticated or the session is invalid.</summary>
public sealed class UnauthorizedException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

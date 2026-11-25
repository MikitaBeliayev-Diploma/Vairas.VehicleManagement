namespace Vairas.VehicleManagement.Business;

/// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
public sealed class ForbiddenException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

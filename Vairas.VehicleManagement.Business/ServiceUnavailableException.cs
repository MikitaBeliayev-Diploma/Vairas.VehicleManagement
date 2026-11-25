namespace Vairas.VehicleManagement.Business;

/// <summary>A dependency or the database is temporarily unavailable.</summary>
public sealed class ServiceUnavailableException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

namespace Vairas.VehicleManagement.Business;

/// <summary>The operation conflicts with the current state (stale revision, duplicate or competing change).</summary>
public sealed class ConflictException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

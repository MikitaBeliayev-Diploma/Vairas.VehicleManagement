namespace Vairas.VehicleManagement.Business;

/// <summary>The request is well formed but violates a business rule.</summary>
public sealed class UnprocessableEntityException(string code, string? message = null, Exception? innerException = null)
    : BusinessException(code, message ?? code, innerException);

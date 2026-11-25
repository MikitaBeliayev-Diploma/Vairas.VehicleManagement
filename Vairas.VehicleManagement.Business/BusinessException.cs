namespace Vairas.VehicleManagement.Business;

/// <summary>
/// An expected application error: a stable machine-readable <see cref="Code"/> and a message that is safe to show to a client.
/// The HTTP status is chosen by the API layer from the category type, never carried by the exception.
/// </summary>
public abstract class BusinessException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public const string DefaultMessage = "The operation could not be completed.";
    public string Code { get; } = code;
}

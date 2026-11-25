namespace Vairas.VehicleManagement.Persistance;

/// <summary>
/// A database failure that is not a recognised, expected business outcome (for example an unexpected foreign-key or check
/// violation). It indicates an implementation defect or an unavailable store, so it is never presented as a business error.
/// Only the SQLSTATE and constraint name are exposed for diagnostics; they contain no row values.
/// </summary>
public sealed class PersistenceFailureException(string? sqlState, string? constraint, Exception innerException)
    : Exception("The database operation failed.", innerException)
{
    public string? SqlState { get; } = sqlState;
    public string? Constraint { get; } = constraint;
}

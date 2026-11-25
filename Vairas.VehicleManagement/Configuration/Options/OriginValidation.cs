namespace Vairas.VehicleManagement.Configuration.Options;

internal static class OriginValidation
{
    /// <summary>An absolute HTTPS URL without credentials, query or fragment.</summary>
    public static bool IsExplicitHttpsOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

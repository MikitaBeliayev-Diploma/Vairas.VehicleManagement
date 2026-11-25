using Microsoft.Extensions.Options;

namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        string.IsNullOrWhiteSpace(options.VehicleManagement)
            ? ValidateOptionsResult.Fail("Configure ConnectionStrings:VehicleManagement externally.")
            : ValidateOptionsResult.Success;
}

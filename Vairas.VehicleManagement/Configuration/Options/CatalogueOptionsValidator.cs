using Microsoft.Extensions.Options;

namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class CatalogueOptionsValidator : IValidateOptions<CatalogueOptions>
{
    public ValidateOptionsResult Validate(string? name, CatalogueOptions options) =>
        string.IsNullOrWhiteSpace(options.Path)
            ? ValidateOptionsResult.Fail("Configure a versioned read-only category catalogue file.")
            : ValidateOptionsResult.Success;
}

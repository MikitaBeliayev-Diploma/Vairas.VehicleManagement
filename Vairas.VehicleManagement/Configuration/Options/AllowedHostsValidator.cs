using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;

namespace Vairas.VehicleManagement.Configuration.Options;

/// <summary>Outside Development/Testing the host filter must name explicit hosts instead of "*".</summary>
public sealed class AllowedHostsValidator(IConfiguration configuration, IHostEnvironment environment) : IValidateOptions<HostFilteringOptions>
{
    public ValidateOptionsResult Validate(string? name, HostFilteringOptions options) =>
        !environment.IsDevelopment() && !environment.IsEnvironment("Testing") && configuration["AllowedHosts"] is null or "" or "*"
            ? ValidateOptionsResult.Fail("Configure AllowedHosts for production.")
            : ValidateOptionsResult.Success;
}

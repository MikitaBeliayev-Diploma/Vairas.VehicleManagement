using Microsoft.Extensions.Options;

namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class DependencyOptionsValidator : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions options)
    {
        if (!new[] { options.AuthBaseUrl, options.UserManagementBaseUrl, options.SchoolBaseUrl, options.SchedulingBaseUrl }.All(OriginValidation.IsExplicitHttpsOrigin))
            return ValidateOptionsResult.Fail("Configure explicit HTTPS dependency origins.");
        if (string.IsNullOrWhiteSpace(options.ClientSecret) || string.IsNullOrWhiteSpace(options.ClientId))
            return ValidateOptionsResult.Fail("Configure the workload client credential externally.");
        return ValidateOptionsResult.Success;
    }
}

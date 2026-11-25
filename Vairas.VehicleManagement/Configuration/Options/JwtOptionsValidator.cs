using Microsoft.Extensions.Options;

namespace Vairas.VehicleManagement.Configuration.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (!OriginValidation.IsExplicitHttpsOrigin(options.Authority) || !OriginValidation.IsExplicitHttpsOrigin(options.Issuer))
            return ValidateOptionsResult.Fail("Configure explicit HTTPS issuer and metadata authority.");
        if (string.IsNullOrWhiteSpace(options.UserAudience) || string.IsNullOrWhiteSpace(options.WorkloadAudience)
            || options.UserAudience == options.WorkloadAudience)
            return ValidateOptionsResult.Fail("Configure distinct user and workload audiences.");
        return ValidateOptionsResult.Success;
    }
}

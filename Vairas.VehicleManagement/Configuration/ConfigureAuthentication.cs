using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.VehicleManagement.Configuration.Options;
using Vairas.VehicleManagement.Middleware;

namespace Vairas.VehicleManagement.Configuration;

/// <summary>Route-specific bearer authentication: user tokens for public routes, workload tokens for /internal.</summary>
public static class ConfigureAuthentication
{
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication("Entry")
            .AddPolicyScheme("Entry", "Route-specific bearer authentication", options =>
                options.ForwardDefaultSelector = http => http.Request.Path.StartsWithSegments("/internal") ? "Workload" : "User")
            .AddJwtBearer("User", _ => { })
            .AddJwtBearer("Workload", _ => { });
        services.AddOptions<JwtBearerOptions>("User").Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            Configure(bearer, jwt.Value, jwt.Value.UserAudience, user: true));
        services.AddOptions<JwtBearerOptions>("Workload").Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            Configure(bearer, jwt.Value, jwt.Value.WorkloadAudience, user: false));
        return services;
    }

    private static void Configure(JwtBearerOptions options, JwtOptions settings, string audience, bool user)
    {
        options.Authority = settings.Authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (user && context.Request.Path.StartsWithSegments("/internal"))
                {
                    var delegated = context.Request.Headers["X-User-Authorization"].ToString();
                    // A missing delegation header must never fall back to the workload bearer token.
                    if (!delegated.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        context.NoResult();
                    else
                        context.Token = delegated[7..];
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.HttpContext.WriteProblemAsync(401, "AUTHENTICATION_REQUIRED", "Authentication required.");
            },
            OnForbidden = context => context.HttpContext.WriteProblemAsync(403, "ACCESS_DENIED", "Access denied.")
        };
    }
}

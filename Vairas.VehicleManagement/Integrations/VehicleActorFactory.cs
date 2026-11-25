using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Vairas.VehicleManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;

namespace Vairas.VehicleManagement.Integrations;

public sealed class VehicleActorFactory(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : IVehicleActorFactory
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task<VehicleActor> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var authenticated = await http.AuthenticateAsync("User");
        if (!authenticated.Succeeded || authenticated.Principal is not ClaimsPrincipal user || !Guid.TryParse(user.FindFirstValue("sub"), out var identity) || identity == Guid.Empty || !Guid.TryParse(user.FindFirstValue("sid"), out var sid) || sid == Guid.Empty)
            throw new UnauthorizedException("INVALID_IDENTITY");
        var started = clock.GetUtcNow();
        var session = await transport.SendAsync<Session>(options.AuthBaseUrl, "internal/v1/sessions/validate", new { identityId = identity, sessionId = sid }, ct);
        if (session.IdentityId != identity || session.SessionId != sid || session.ValidUntil <= clock.GetUtcNow())
            throw VehicleErrors.Dependency();
        if (!session.Active)
            throw new UnauthorizedException("SESSION_INVALID");
        var raw = http.Request.Headers.Authorization.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("INVALID_IDENTITY");
        var profile = await transport.SendAsync<SelfProfile>(options.UserManagementBaseUrl, "api/v1/users/me", null, ct, raw[7..]);
        if (profile.UserId == Guid.Empty || profile.Status != "Active")
            throw VehicleErrors.Denied();
        var actor = new VehicleActor(profile.UserId, session.ValidUntil < started.AddSeconds(30) ? session.ValidUntil : started.AddSeconds(30));
        VehicleRules.Fresh(actor, clock.GetUtcNow());
        return actor;
    }

    private sealed record Session(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
    private sealed record SelfProfile(Guid UserId, string Status);
}

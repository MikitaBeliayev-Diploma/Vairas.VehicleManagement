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
public sealed class UserProfilesClient(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings) : IUserProfiles
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task RequireActiveAsync(Guid userId, CancellationToken ct)
    {
        var result = await transport.SendAsync<Status>(options.UserManagementBaseUrl, $"internal/v1/users/{userId:D}/status", null, ct);
        if (result.UserId != userId)
            throw VehicleErrors.Dependency();
        if (!result.Active)
            throw VehicleErrors.Denied();
    }

    public Task<ProfileBinding> TeacherAsync(Guid teacherId, CancellationToken ct) => transport.SendAsync<ProfileBinding>(options.UserManagementBaseUrl, $"internal/v1/profiles/teacher/{teacherId:D}", null, ct);
    private sealed record Status(Guid UserId, bool Active);
}

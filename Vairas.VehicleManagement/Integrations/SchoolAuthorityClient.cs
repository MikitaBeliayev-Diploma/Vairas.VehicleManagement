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
public sealed class SchoolAuthorityClient(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings) : ISchoolAuthority
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public Task<VehicleAuthorityResponse> ValidateAsync(VehicleAuthorityRequest request, CancellationToken ct) => transport.SendAsync<VehicleAuthorityResponse>(options.SchoolBaseUrl, "internal/v1/vehicle-authority/validate", request, ct);
}

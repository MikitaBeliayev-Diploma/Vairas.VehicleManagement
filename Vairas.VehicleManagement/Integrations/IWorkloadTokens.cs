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

public interface IWorkloadTokens
{
    Task<string> GetAsync(CancellationToken ct);
}

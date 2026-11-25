using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Integrations;

public sealed class DependencyTransport(IHttpClientFactory clients, IWorkloadTokens tokens) : IDependencyTransport
{
    public async Task<HttpResponseMessage> RequestAsync(string origin, string route, object? body, CancellationToken ct, string? userToken = null)
    {
        try
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(new Uri(origin), route));
            if (body is not null)
                request.Content = JsonContent.Create(body, options: VehicleRules.Json);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken ?? await tokens.GetAsync(ct));
            return await clients.CreateClient("dependencies").SendAsync(request, ct);
        }
        catch (Exception e)when (e is HttpRequestException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw VehicleErrors.Dependency();
        }
    }

    public async Task<T> SendAsync<T>(string origin, string route, object? body, CancellationToken ct, string? userToken = null)
    {
        using var response = await RequestAsync(origin, route, body, ct, userToken);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw VehicleErrors.Denied();
        if (response.StatusCode == HttpStatusCode.Unauthorized && userToken is not null)
            throw new UnauthorizedException("SESSION_INVALID");
        if (!response.IsSuccessStatusCode)
            throw VehicleErrors.Dependency();
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(VehicleRules.Json, ct) ?? throw VehicleErrors.Dependency();
        }
        catch (JsonException)
        {
            throw VehicleErrors.Dependency();
        }
    }
}

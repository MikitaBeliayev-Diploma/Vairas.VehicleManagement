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

public sealed class WorkloadTokens(IHttpClientFactory clients, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : IWorkloadTokens, IDisposable
{
    private readonly DependencyOptions options = optionsSettings.Value;
    private readonly SemaphoreSlim mutex = new(1, 1);
    private string? token;
    private DateTimeOffset expires;
    public async Task<string> GetAsync(CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        try
        {
            if (token is not null && expires > clock.GetUtcNow().AddSeconds(30))
                return token;
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.AuthBaseUrl), "connect/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret, ["scope"] = options.Scope })
            };
            using var response = await clients.CreateClient("dependencies").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw VehicleErrors.Dependency();
            var result = await response.Content.ReadFromJsonAsync<Token>(ct);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || !string.Equals(result.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) || result.ExpiresIn <= 30)
                throw VehicleErrors.Dependency();
            token = result.AccessToken;
            expires = clock.GetUtcNow().AddSeconds(Math.Min(result.ExpiresIn, 3600));
            return token;
        }
        catch (Exception e)when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw VehicleErrors.Dependency();
        }
        finally
        {
            mutex.Release();
        }
    }

    public void Dispose() => mutex.Dispose();
    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("token_type")] string TokenType, [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

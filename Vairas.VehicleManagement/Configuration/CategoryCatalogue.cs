using System.Text.Json;
using Microsoft.Extensions.Options;
using Vairas.VehicleManagement.Configuration.Options;
using Vairas.VehicleManagement.Business;

namespace Vairas.VehicleManagement.Configuration;

public sealed class CategoryCatalogue : ICategoryCatalogue
{
    private readonly HashSet<string> codes;
    public string Version { get; }
    public bool Contains(string code) => codes.Contains(code);
    public CategoryCatalogue(IOptions<CatalogueOptions> options, IHostEnvironment environment)
    {
        var path = options.Value.Path;
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Configure a versioned read-only category catalogue file.");
        var fullPath = Path.IsPathFullyQualified(path) ? path : Path.Combine(environment.ContentRootPath, path);
        var data = JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(fullPath), VehicleRules.Json)
            ?? throw new InvalidOperationException("Invalid category catalogue.");
        Version = VehicleRules.Text(data.Version, 120);
        _ = VehicleRules.Text(data.Source, 1024);
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && (data.Synthetic || !data.Verified || data.ReviewedAt is null))
            throw new InvalidOperationException("Production requires a verified non-synthetic catalogue with source and review date.");
        if (data.Codes is null || data.Codes.Length == 0 || data.Codes.Length > 1000) throw new InvalidOperationException("Empty or oversized category catalogue.");
        codes = data.Codes.Select(x => VehicleRules.Text(x, 16)).ToHashSet(StringComparer.Ordinal);
        if (codes.Count != data.Codes.Length || codes.Any(x => x != x.ToUpperInvariant())) throw new InvalidOperationException("Category codes must be unique canonical uppercase values.");
    }
    private sealed record CatalogueFile(string Version, string Source, bool Synthetic, bool Verified, DateTimeOffset? ReviewedAt, string[] Codes);
}

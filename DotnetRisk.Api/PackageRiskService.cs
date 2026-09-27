using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace DotnetRisk.Api;

public sealed class PackageRiskService(HttpClient httpClient, IMemoryCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PackageRiskReport> AnalyzeAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var normalizedId = packageId.Trim().ToLowerInvariant();
        var normalizedVersion = version.Trim().ToLowerInvariant();
        var cacheKey = $"risk:{normalizedId}:{normalizedVersion}";

        if (cache.TryGetValue(cacheKey, out PackageRiskReport? cached) && cached is not null)
        {
            return cached;
        }

        var versionsTask = GetVersionsAsync(normalizedId, cancellationToken);
        var vulnerabilitiesTask = GetVulnerabilitiesAsync(normalizedId, normalizedVersion, cancellationToken);
        await Task.WhenAll(versionsTask, vulnerabilitiesTask);

        var versions = await versionsTask;
        var vulnerabilities = await vulnerabilitiesTask;
        var latestStable = versions.LastOrDefault(item => !item.Contains('-', StringComparison.Ordinal));
        var isOutdated = latestStable is not null &&
            !string.Equals(latestStable, normalizedVersion, StringComparison.OrdinalIgnoreCase);

        var riskScore = Math.Min(100, (vulnerabilities.Count * 25) + (isOutdated ? 10 : 0));
        var report = new PackageRiskReport(
            normalizedId,
            normalizedVersion,
            latestStable,
            isOutdated,
            riskScore,
            GetRiskLevel(riskScore),
            vulnerabilities,
            DateTimeOffset.UtcNow,
            [
                $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(normalizedId)}/index.json",
                "https://api.osv.dev/v1/query"
            ]);

        cache.Set(cacheKey, report, TimeSpan.FromHours(6));
        return report;
    }

    public async Task<ProjectAuditReport> AuditAsync(
        ProjectAuditRequest request,
        CancellationToken cancellationToken)
    {
        var reports = await Task.WhenAll(request.Packages.Select(item =>
            AnalyzeAsync(item.PackageId, item.Version, cancellationToken)));
        var orderedReports = reports
            .OrderByDescending(item => item.RiskScore)
            .ThenBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var remediationPlan = orderedReports
            .Where(item => item.RiskScore > 0)
            .Select((item, index) => new RemediationAction(
                index + 1,
                item.PackageId,
                item.Version,
                item.LatestStableVersion,
                item.Vulnerabilities.Count > 0
                    ? $"Resolve {item.Vulnerabilities.Count} known vulnerability record(s)."
                    : "Review and update an outdated dependency."))
            .ToArray();
        var overallRiskScore = orderedReports.Length == 0
            ? 0
            : Math.Min(100, orderedReports.Max(item => item.RiskScore) +
                Math.Min(20, orderedReports.Count(item => item.Vulnerabilities.Count > 0) * 5));

        return new ProjectAuditReport(
            orderedReports.Length,
            orderedReports.Count(item => item.Vulnerabilities.Count > 0),
            orderedReports.Count(item => item.IsOutdated),
            overallRiskScore,
            GetRiskLevel(overallRiskScore),
            remediationPlan,
            orderedReports,
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<string>> GetVersionsAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(packageId)}/index.json";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new PackageNotFoundException(packageId);
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<NuGetVersionsResponse>(
            stream,
            JsonOptions,
            cancellationToken);
        return payload?.Versions ?? [];
    }

    private async Task<IReadOnlyList<VulnerabilitySummary>> GetVulnerabilitiesAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var request = new OsvQueryRequest(version, new OsvPackage(packageId, "NuGet"));
        using var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");
        using var response = await httpClient.PostAsync("https://api.osv.dev/v1/query", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OsvQueryResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        return payload?.Vulns?
            .Select(item => new VulnerabilitySummary(
                item.Id,
                item.Summary,
                item.Aliases ?? [],
                item.Modified,
                item.References?.Select(reference => reference.Url).Where(url => url is not null).Take(5).Cast<string>().ToArray() ?? []))
            .ToArray() ?? [];
    }

    private static string GetRiskLevel(int score) => score switch
    {
        >= 75 => "critical",
        >= 50 => "high",
        >= 25 => "medium",
        > 0 => "low",
        _ => "none"
    };

    private sealed record NuGetVersionsResponse(IReadOnlyList<string> Versions);
    private sealed record OsvQueryRequest(string Version, OsvPackage Package);
    private sealed record OsvPackage(string Name, string Ecosystem);
    private sealed record OsvQueryResponse(IReadOnlyList<OsvVulnerability>? Vulns);
    private sealed record OsvVulnerability(
        string Id,
        string? Summary,
        IReadOnlyList<string>? Aliases,
        DateTimeOffset Modified,
        IReadOnlyList<OsvReference>? References);
    private sealed record OsvReference(string? Url);
}

public sealed class PackageNotFoundException(string packageId)
    : Exception($"NuGet package '{packageId}' was not found.");

using Microsoft.Extensions.Caching.Memory;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Text.Json;

namespace DotnetRisk.Api;

public sealed class UpgradePlannerService(HttpClient httpClient, IMemoryCache cache)
{
    private const long MaximumPackageBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly FrameworkReducer FrameworkReducer = new();
    private static readonly SemaphoreSlim PackageDownloadSlots = new(2, 2);

    public async Task<UpgradePlanReport> PlanAsync(
        UpgradePlanRequest request,
        CancellationToken cancellationToken)
    {
        var targetFramework = NuGetFramework.ParseFolder(request.TargetFramework.ToLowerInvariant());
        if (targetFramework.IsUnsupported)
        {
            throw new UnsupportedTargetFrameworkException(request.TargetFramework);
        }

        var assessments = await Task.WhenAll(request.Packages.Select(item =>
            AssessAsync(item, targetFramework, cancellationToken)));
        var ordered = assessments
            .OrderByDescending(item => item.LatestCompatibility == "incompatible")
            .ThenByDescending(item => item.LatestStableVersion != item.CurrentVersion)
            .ThenBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UpgradePlanReport(
            targetFramework.GetShortFolderName(),
            ordered.Length,
            ordered.Count(item => item.LatestStableVersion != item.CurrentVersion),
            ordered.Count(item => item.LatestCompatibility == "incompatible"),
            ordered,
            DateTimeOffset.UtcNow);
    }

    private async Task<PackageUpgradeAssessment> AssessAsync(
        PackageReference package,
        NuGetFramework targetFramework,
        CancellationToken cancellationToken)
    {
        var packageId = package.PackageId.ToLowerInvariant();
        var currentVersion = NuGetVersion.Parse(package.Version).ToNormalizedString();
        var latestVersion = await GetLatestStableVersionAsync(packageId, cancellationToken);
        var current = await GetCompatibilityAsync(packageId, currentVersion, targetFramework, cancellationToken);
        var latest = latestVersion is null || latestVersion == currentVersion
            ? current
            : await GetCompatibilityAsync(packageId, latestVersion, targetFramework, cancellationToken);

        var recommendation = (latestVersion, latest.Status) switch
        {
            (null, _) => "Keep the current version; no stable release was found.",
            (_, "incompatible") => $"Do not upgrade automatically to {latestVersion}; no compatible assets were found for {targetFramework.GetShortFolderName()}.",
            (_, "unknown") => $"Review {latestVersion} manually; the package contains no framework-specific assets that can be evaluated.",
            _ when latestVersion == currentVersion => "No stable package upgrade is available.",
            _ => $"Upgrade to {latestVersion} and run restore, build, and tests."
        };

        return new PackageUpgradeAssessment(
            packageId,
            currentVersion,
            latestVersion,
            current.Status,
            latest.Status,
            current.SelectedFramework,
            latest.SelectedFramework,
            recommendation);
    }

    private async Task<string?> GetLatestStableVersionAsync(
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
        var payload = await JsonSerializer.DeserializeAsync<NuGetVersionsResponse>(stream, JsonOptions, cancellationToken);
        return payload?.Versions
            .Select(NuGetVersion.Parse)
            .Where(version => !version.IsPrerelease)
            .OrderByDescending(version => version)
            .FirstOrDefault()
            ?.ToNormalizedString();
    }

    private async Task<CompatibilityResult> GetCompatibilityAsync(
        string packageId,
        string version,
        NuGetFramework targetFramework,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"compatibility:{packageId}:{version}:{targetFramework.GetShortFolderName()}";
        if (cache.TryGetValue(cacheKey, out CompatibilityResult? cached) && cached is not null)
        {
            return cached;
        }

        var url = $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(packageId)}/{Uri.EscapeDataString(version.ToLowerInvariant())}/{Uri.EscapeDataString(packageId)}.{Uri.EscapeDataString(version.ToLowerInvariant())}.nupkg";
        await PackageDownloadSlots.WaitAsync(cancellationToken);
        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new PackageVersionNotFoundException(packageId, version);
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumPackageBytes)
            {
                throw new PackageTooLargeException(packageId, version);
            }

            await using var packageStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            await packageStream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaximumPackageBytes)
            {
                throw new PackageTooLargeException(packageId, version);
            }

            buffer.Position = 0;
            using var reader = new PackageArchiveReader(buffer, leaveStreamOpen: false);
            var libItems = await reader.GetLibItemsAsync(cancellationToken);
            var referenceItems = await reader.GetReferenceItemsAsync(cancellationToken);
            var dependencyGroups = await reader.GetPackageDependenciesAsync(cancellationToken);
            var frameworks = libItems.Select(item => item.TargetFramework)
                .Concat(referenceItems.Select(item => item.TargetFramework))
                .Concat(dependencyGroups.Select(item => item.TargetFramework))
                .Where(framework => !framework.IsUnsupported)
                .Distinct(NuGetFramework.Comparer)
                .ToArray();

            CompatibilityResult result;
            if (frameworks.Length == 0)
            {
                result = new CompatibilityResult("unknown", null);
            }
            else
            {
                var nearest = FrameworkReducer.GetNearest(targetFramework, frameworks);
                result = nearest is null
                    ? new CompatibilityResult("incompatible", null)
                    : new CompatibilityResult("compatible", nearest.GetShortFolderName());
            }

            cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            return result;
        }
        finally
        {
            PackageDownloadSlots.Release();
        }
    }

    private sealed record NuGetVersionsResponse(IReadOnlyList<string> Versions);
    private sealed record CompatibilityResult(string Status, string? SelectedFramework);
}

public sealed class UnsupportedTargetFrameworkException(string targetFramework)
    : Exception($"Target framework '{targetFramework}' is not supported.");

public sealed class PackageVersionNotFoundException(string packageId, string version)
    : Exception($"NuGet package '{packageId}' version '{version}' was not found.");

public sealed class PackageTooLargeException(string packageId, string version)
    : Exception($"NuGet package '{packageId}' version '{version}' exceeds the analysis size limit.");

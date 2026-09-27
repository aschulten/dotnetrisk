using System.Text.RegularExpressions;

namespace DotnetRisk.Api;

public sealed record PackageRiskReport(
    string PackageId,
    string Version,
    string? LatestStableVersion,
    bool IsOutdated,
    int RiskScore,
    string RiskLevel,
    IReadOnlyList<VulnerabilitySummary> Vulnerabilities,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> Sources);

public sealed record VulnerabilitySummary(
    string Id,
    string? Summary,
    IReadOnlyList<string> Aliases,
    DateTimeOffset Modified,
    IReadOnlyList<string> References);

public sealed record ProjectAuditRequest(IReadOnlyList<PackageReference> Packages);

public sealed record PackageReference(string PackageId, string Version);

public sealed record ProjectAuditReport(
    int PackageCount,
    int VulnerablePackageCount,
    int OutdatedPackageCount,
    int OverallRiskScore,
    string OverallRiskLevel,
    IReadOnlyList<RemediationAction> RemediationPlan,
    IReadOnlyList<PackageRiskReport> Packages,
    DateTimeOffset GeneratedAt);

public sealed record RemediationAction(
    int Priority,
    string PackageId,
    string CurrentVersion,
    string? TargetVersion,
    string Reason);

public sealed record UpgradePlanRequest(
    string TargetFramework,
    IReadOnlyList<PackageReference> Packages);

public sealed record UpgradePlanReport(
    string TargetFramework,
    int PackageCount,
    int UpgradeCount,
    int IncompatibleUpgradeCount,
    IReadOnlyList<PackageUpgradeAssessment> Actions,
    DateTimeOffset GeneratedAt);

public sealed record PackageUpgradeAssessment(
    string PackageId,
    string CurrentVersion,
    string? LatestStableVersion,
    string CurrentCompatibility,
    string LatestCompatibility,
    string? SelectedCurrentAssetFramework,
    string? SelectedLatestAssetFramework,
    string Recommendation);

public static partial class PackageInput
{
    public const int MaximumProjectPackages = 25;
    public const int MaximumUpgradePackages = 10;

    public static bool IsValid(string packageId, string version) =>
        !string.IsNullOrWhiteSpace(packageId) &&
        !string.IsNullOrWhiteSpace(version) &&
        packageId.Length <= 128 &&
        version.Length <= 64 &&
        PackageIdPattern().IsMatch(packageId) &&
        VersionPattern().IsMatch(version);

    public static bool IsValid(ProjectAuditRequest? request) =>
        request is not null &&
        request.Packages is { Count: > 0 and <= MaximumProjectPackages } &&
        request.Packages.All(item => IsValid(item.PackageId, item.Version));

    public static bool IsValid(UpgradePlanRequest? request) =>
        request is not null &&
        TargetFrameworkPattern().IsMatch(request.TargetFramework) &&
        request.Packages is { Count: > 0 and <= MaximumUpgradePackages } &&
        request.Packages.All(item => IsValid(item.PackageId, item.Version));

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();

    [GeneratedRegex("^[A-Za-z0-9.+-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^net(?:standard|coreapp)?[0-9]+\\.[0-9]+(?:-[A-Za-z0-9.-]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkPattern();
}

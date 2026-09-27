using DotnetRisk.Api;

namespace DotnetRisk.Api.Tests;

public sealed class PackageInputTests
{
    [Theory]
    [InlineData("Newtonsoft.Json", "13.0.3")]
    [InlineData("Microsoft.Extensions.Http", "8.0.0-preview.1")]
    public void IsValid_AcceptsNuGetIdentifiers(string packageId, string version)
    {
        Assert.True(PackageInput.IsValid(packageId, version));
    }

    [Theory]
    [InlineData("../secret", "1.0.0")]
    [InlineData("valid", "1.0.0/../../")]
    [InlineData("", "1.0.0")]
    public void IsValid_RejectsUnsafeInput(string packageId, string version)
    {
        Assert.False(PackageInput.IsValid(packageId, version));
    }

    [Fact]
    public void IsValid_AcceptsProjectWithinLimit()
    {
        var request = new ProjectAuditRequest(
            [new PackageReference("Newtonsoft.Json", "13.0.3")]);

        Assert.True(PackageInput.IsValid(request));
    }

    [Fact]
    public void IsValid_RejectsProjectAboveLimit()
    {
        var packages = Enumerable.Range(0, PackageInput.MaximumProjectPackages + 1)
            .Select(index => new PackageReference($"Package.{index}", "1.0.0"))
            .ToArray();

        Assert.False(PackageInput.IsValid(new ProjectAuditRequest(packages)));
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net10.0-windows")]
    [InlineData("netstandard2.0")]
    public void IsValid_AcceptsUpgradeTargetFramework(string targetFramework)
    {
        var request = new UpgradePlanRequest(
            targetFramework,
            [new PackageReference("Newtonsoft.Json", "13.0.3")]);

        Assert.True(PackageInput.IsValid(request));
    }

    [Theory]
    [InlineData("net8")]
    [InlineData("javascript")]
    [InlineData("../net8.0")]
    public void IsValid_RejectsInvalidUpgradeTargetFramework(string targetFramework)
    {
        var request = new UpgradePlanRequest(
            targetFramework,
            [new PackageReference("Newtonsoft.Json", "13.0.3")]);

        Assert.False(PackageInput.IsValid(request));
    }
}

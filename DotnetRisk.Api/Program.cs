using DotnetRisk.Api;
using x402;
using x402.Core.Enums;
using x402.Core.Models;
using x402.Core.Models.v2;
using x402.EndpointFilters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var facilitatorUrl = builder.Configuration["Payment:FacilitatorUrl"]
    ?? throw new InvalidOperationException("Payment:FacilitatorUrl is required.");
var paymentNetwork = builder.Configuration["Payment:Network"]
    ?? throw new InvalidOperationException("Payment:Network is required.");
var paymentAsset = builder.Configuration["Payment:Asset"]
    ?? throw new InvalidOperationException("Payment:Asset is required.");
var paymentRecipient = builder.Configuration["Payment:PayTo"]
    ?? throw new InvalidOperationException("Payment:PayTo is required.");
var publicBaseUrl = builder.Configuration["Payment:PublicBaseUrl"]?.TrimEnd('/');

builder.Services.AddX402().WithHttpFacilitator(facilitatorUrl);
builder.Services.AddHttpClient<PackageRiskService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetRisk/0.1");
});
builder.Services.AddHttpClient<UpgradePlannerService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DotnetRisk/0.1");
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "dotnet-risk",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/demo", async (PackageRiskService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.AnalyzeAsync("newtonsoft.json", "12.0.1", cancellationToken)));

app.MapGet("/product", () => Results.Ok(new
{
    product = "DotnetRisk",
    nugetAuditPriceUsd = 0.05m,
    upgradePlanPriceUsd = 0.25m,
    maximumPackages = PackageInput.MaximumProjectPackages,
    input = "NuGet package IDs and versions only; source code is not collected.",
    output = "Prioritized dependency risk and remediation plan."
}));

app.MapPost("/v1/upgrade-plans", async (
    UpgradePlanRequest request,
    UpgradePlannerService service,
    CancellationToken cancellationToken) =>
{
    if (!PackageInput.IsValid(request))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [$"Provide a valid target framework and between 1 and {PackageInput.MaximumUpgradePackages} package references."]
        });
    }

    try
    {
        return Results.Ok(await service.PlanAsync(request, cancellationToken));
    }
    catch (Exception exception) when (exception is PackageNotFoundException or
                                      PackageVersionNotFoundException or
                                      UnsupportedTargetFrameworkException)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (PackageTooLargeException exception)
    {
        return Results.Problem(
            title: exception.Message,
            statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            title: "Upstream data source unavailable",
            statusCode: StatusCodes.Status502BadGateway);
    }
})
.RequireX402Payment(
    CreatePaymentRequirement(
        path: "/v1/upgrade-plans",
        amount: "250000",
        description: "Analyze NuGet package compatibility for a target .NET framework and return a prioritized upgrade plan.",
        tags: ["dotnet", "nuget", "upgrade", "compatibility"],
        exampleBody: new
        {
            targetFramework = "net10.0",
            packages = new[] { new { packageId = "Newtonsoft.Json", version = "12.0.1" } }
        }),
    SettlementMode.Pessimistic);

app.MapPost("/v1/audits", async (
    ProjectAuditRequest request,
    PackageRiskService service,
    CancellationToken cancellationToken) =>
{
    if (!PackageInput.IsValid(request))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["packages"] = [$"Provide between 1 and {PackageInput.MaximumProjectPackages} valid package references."]
        });
    }

    try
    {
        return Results.Ok(await service.AuditAsync(request, cancellationToken));
    }
    catch (PackageNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            title: "Upstream data source unavailable",
            statusCode: StatusCodes.Status502BadGateway);
    }
})
.RequireX402Payment(
    CreatePaymentRequirement(
        path: "/v1/audits",
        amount: "50000",
        description: "Audit NuGet package versions for known vulnerabilities and return a prioritized remediation plan.",
        tags: ["dotnet", "nuget", "security", "vulnerability"],
        exampleBody: new
        {
            packages = new[] { new { packageId = "Newtonsoft.Json", version = "12.0.1" } }
        }),
    SettlementMode.Pessimistic);

app.MapGet("/v1/risk/{packageId}/{version}", async (
    string packageId,
    string version,
    PackageRiskService service,
    CancellationToken cancellationToken) =>
{
    if (!PackageInput.IsValid(packageId, version))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["package"] = ["Package ID and version contain unsupported characters."]
        });
    }

    try
    {
        return Results.Ok(await service.AnalyzeAsync(packageId, version, cancellationToken));
    }
    catch (PackageNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            title: "Upstream data source unavailable",
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

PaymentRequiredInfo CreatePaymentRequirement(
    string path,
    string amount,
    string description,
    string[] tags,
    object exampleBody) => new()
{
    Resource = new ResourceInfoBasic
    {
        Resource = publicBaseUrl is null ? null : $"{publicBaseUrl}{path}",
        MimeType = "application/json",
        Description = description,
        ServiceName = "DotnetRisk",
        Tags = tags.ToList()
    },
    Accepts =
    [
        new PaymentRequirementsBasic
        {
            Amount = amount,
            Asset = paymentAsset,
            PayTo = paymentRecipient,
            Network = paymentNetwork,
            MaxTimeoutSeconds = 60
        }
    ],
    Discoverable = true,
    Extensions = CreateBazaarExtension(exampleBody)
};

Dictionary<string, ExtensionData> CreateBazaarExtension(object exampleBody) => new()
{
    ["bazaar"] = new ExtensionData
    {
        Info = new
        {
            input = new
            {
                type = "http",
                method = "POST",
                bodyType = "json",
                body = exampleBody
            }
        },
        Schema = new
        {
            type = "object",
            properties = new
            {
                input = new
                {
                    type = "object",
                    properties = new
                    {
                        type = new { type = "string", @const = "http" },
                        method = new { type = "string", @enum = new[] { "POST", "PUT", "PATCH" } },
                        bodyType = new { type = "string", @enum = new[] { "json", "form-data", "text" } },
                        body = new { type = "object" }
                    },
                    required = new[] { "type", "method", "bodyType", "body" },
                    additionalProperties = false
                }
            },
            required = new[] { "input" }
        }
    }
};

public partial class Program;

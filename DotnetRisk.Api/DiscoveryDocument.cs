using System.Text.Json.Nodes;

namespace DotnetRisk.Api;

internal static class DiscoveryDocument
{
    public static JsonObject CreateOpenApi(string baseUrl, string paymentRecipient) => new()
    {
        ["openapi"] = "3.0.3",
        ["info"] = new JsonObject
        {
            ["title"] = "DotnetRisk NuGet Security and .NET Upgrade API",
            ["version"] = "1.0.0",
            ["description"] = "Paid NuGet vulnerability audits and .NET target-framework upgrade plans for coding agents. Source code is never collected.",
            ["contact"] = new JsonObject
            {
                ["name"] = "DotnetRisk",
                ["url"] = "https://github.com/aschulten/dotnetrisk"
            },
            ["x-guidance"] = "Choose the security audit for exact-version vulnerability remediation. Choose the upgrade planner for target-framework compatibility and package upgrade ordering. Submit package IDs and versions only."
        },
        ["servers"] = new JsonArray(new JsonObject { ["url"] = baseUrl }),
        ["x-discovery"] = new JsonObject
        {
            ["ownershipProofs"] = new JsonArray(paymentRecipient)
        },
        ["paths"] = new JsonObject
        {
            ["/v1/upgrade-plans"] = CreatePaidOperation(
                "Create a .NET upgrade plan",
                "Analyze NuGet package compatibility for a target framework and return a prioritized upgrade plan.",
                "0.25",
                [".NET", "NuGet", "package upgrade", "compatibility"],
                new JsonObject
                {
                    ["targetFramework"] = new JsonObject { ["type"] = "string", ["example"] = "net10.0" },
                    ["packages"] = CreatePackagesSchema(10)
                },
                ["targetFramework", "packages"]),
            ["/v1/audits"] = CreatePaidOperation(
                "Audit NuGet package security",
                "Audit exact NuGet package versions for known vulnerabilities and remediation options.",
                "0.05",
                [".NET", "NuGet", "dependency security", "vulnerability"],
                new JsonObject
                {
                    ["packages"] = CreatePackagesSchema(25)
                },
                ["packages"])
        },
        ["components"] = new JsonObject
        {
            ["securitySchemes"] = new JsonObject
            {
                ["x402Payment"] = new JsonObject
                {
                    ["type"] = "apiKey",
                    ["in"] = "header",
                    ["name"] = "PAYMENT-SIGNATURE",
                    ["description"] = "x402 v2 payment signature returned after the initial 402 challenge."
                }
            }
        }
    };

    public static JsonObject CreateWellKnown(string baseUrl, string paymentRecipient) => new()
    {
        ["version"] = 1,
        ["resources"] = new JsonArray(
            $"{baseUrl}/v1/upgrade-plans",
            $"{baseUrl}/v1/audits"),
        ["ownershipProofs"] = new JsonArray(paymentRecipient)
    };

    private static JsonObject CreatePaidOperation(
        string summary,
        string description,
        string price,
        string[] tags,
        JsonObject properties,
        string[] required) => new()
    {
        ["post"] = new JsonObject
        {
            ["summary"] = summary,
            ["description"] = description,
            ["tags"] = new JsonArray(tags.Select(value => JsonValue.Create(value)).ToArray()),
            ["security"] = new JsonArray(new JsonObject { ["x402Payment"] = new JsonArray() }),
            ["x-payment-info"] = new JsonObject
            {
                ["protocols"] = new JsonArray("x402"),
                ["price"] = new JsonObject
                {
                    ["mode"] = "fixed",
                    ["currency"] = "USD",
                    ["amount"] = price
                }
            },
            ["requestBody"] = new JsonObject
            {
                ["required"] = true,
                ["content"] = new JsonObject
                {
                    ["application/json"] = new JsonObject
                    {
                        ["schema"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = properties,
                            ["required"] = new JsonArray(required.Select(value => JsonValue.Create(value)).ToArray()),
                            ["additionalProperties"] = false
                        }
                    }
                }
            },
            ["responses"] = new JsonObject
            {
                ["200"] = new JsonObject { ["description"] = "Analysis completed." },
                ["402"] = new JsonObject { ["description"] = "x402 payment required." }
            }
        }
    };

    private static JsonObject CreatePackagesSchema(int maximumItems) => new()
    {
        ["type"] = "array",
        ["minItems"] = 1,
        ["maxItems"] = maximumItems,
        ["items"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["packageId"] = new JsonObject { ["type"] = "string", ["example"] = "Newtonsoft.Json" },
                ["version"] = new JsonObject { ["type"] = "string", ["example"] = "12.0.1" }
            },
            ["required"] = new JsonArray("packageId", "version"),
            ["additionalProperties"] = false
        }
    };
}

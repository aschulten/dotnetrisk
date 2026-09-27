using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DotnetRisk.Api.Tests;

public sealed class PaymentGateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public PaymentGateTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task UpgradePlan_WithoutPayment_ReturnsMainnetPaymentRequirement()
    {
        using var response = await client.PostAsJsonAsync("/v1/upgrade-plans", new
        {
            targetFramework = "net10.0",
            packages = new[] { new { packageId = "Newtonsoft.Json", version = "12.0.1" } }
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        using var paymentRequired = DecodePaymentRequired(response);
        var requirement = paymentRequired.RootElement.GetProperty("accepts")[0];

        Assert.Equal(2, paymentRequired.RootElement.GetProperty("x402Version").GetInt32());
        Assert.Equal("eip155:8453", requirement.GetProperty("network").GetString());
        Assert.Equal("250000", requirement.GetProperty("amount").GetString());
        Assert.Equal(
            "0x9bb8c86df572b0102ab8b99ba9d480d9751457b5",
            requirement.GetProperty("payTo").GetString());
        Assert.True(paymentRequired.RootElement.GetProperty("extensions").TryGetProperty("bazaar", out _));
    }

    [Fact]
    public async Task Audit_WithMalformedPayment_ReturnsPaymentRequired()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/audits")
        {
            Content = JsonContent.Create(new
            {
                packages = new[] { new { packageId = "Newtonsoft.Json", version = "12.0.1" } }
            })
        };
        request.Headers.Add("PAYMENT-SIGNATURE", "not-base64");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        using var paymentRequired = DecodePaymentRequired(response);
        Assert.Equal(
            "50000",
            paymentRequired.RootElement.GetProperty("accepts")[0].GetProperty("amount").GetString());
    }

    private static JsonDocument DecodePaymentRequired(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("PAYMENT-REQUIRED", out var values));
        var encoded = Assert.Single(values);
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
    }
}

namespace FleetOps.IntegrationTests.Security;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Api.Extensions;
using FleetOps.IntegrationTests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public class RateLimitingTests : IClassFixture<FleetOpsApiFactory>
{
    private readonly FleetOpsApiFactory _factory;

    public RateLimitingTests(FleetOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RateLimiter_WhenExceedingConfiguredPermitLimit_Returns429TooManyRequestsWithProblemDetails()
    {
        // Arrange: Custom factory with strict rate limiting: 3 permits per 2-second window
        using var throttledFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PermitLimit", "3");
            builder.UseSetting("RateLimiting:WindowSeconds", "2");
        });

        using var client = throttledFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "FleetManager");

        // Act & Assert: First 3 requests must succeed
        for (var i = 1; i <= 3; i++)
        {
            var request = new RegisterVehicleRequest(
                $"RLM00{i}",
                "Van",
                "Mercedes",
                "Sprinter",
                2023,
                1000,
                3500m);

            var response = await client.PostAsJsonAsync("/api/vehicles", request);
            Assert.True(
                response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict,
                $"Request {i} unexpectedly rejected with status {response.StatusCode}");
        }

        // Act 2: 4th request within the same 2-second window must be rejected
        var excessRequest = new RegisterVehicleRequest(
            "RLM004",
            "Van",
            "Mercedes",
            "Sprinter",
            2023,
            1000,
            3500m);

        var rateLimitedResponse = await client.PostAsJsonAsync("/api/vehicles", excessRequest);

        // Assert: HTTP 429 Too Many Requests with RFC 9457 ProblemDetails
        Assert.Equal((HttpStatusCode)429, rateLimitedResponse.StatusCode);
        Assert.Equal("application/problem+json", rateLimitedResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rateLimitedResponse.Headers.RetryAfter);

        var content = await rateLimitedResponse.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
        Assert.Contains("Rate limit exceeded", problem.Detail);

        // Act 3: Wait for the 2-second window to reset
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Assert: 5th request after window reset is accepted
        var recoveredRequest = new RegisterVehicleRequest(
            "RLM005",
            "Van",
            "Mercedes",
            "Sprinter",
            2023,
            1000,
            3500m);

        var recoveredResponse = await client.PostAsJsonAsync("/api/vehicles", recoveredRequest);
        Assert.True(
            recoveredResponse.StatusCode == HttpStatusCode.Created || recoveredResponse.StatusCode == HttpStatusCode.OK || recoveredResponse.StatusCode == HttpStatusCode.Conflict,
            $"Recovered request unexpectedly rejected with status {recoveredResponse.StatusCode}");
    }

    [Fact]
    public async Task HealthProbes_AreExemptFromControllerRateLimiting()
    {
        using var throttledFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PermitLimit", "2");
            builder.UseSetting("RateLimiting:WindowSeconds", "5");
        });

        using var client = throttledFactory.CreateClient();

        // Send 10 rapid requests to /health/live
        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class HealthCheckAndExceptionApiTests : BaseApiTest
{
    public HealthCheckAndExceptionApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task LivenessHealthCheck_Returns200OK()
    {
        var response = await Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessHealthCheck_WhenDatabaseIsRunning_Returns200OK()
    {
        var response = await Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
        Assert.DoesNotContain("Password", content);
        Assert.DoesNotContain("5433", content);
    }

    [Fact]
    public async Task ReadinessHealthCheck_WhenDatabaseIsUnavailable_Returns503ServiceUnavailable_WithSanitizedResponse()
    {
        using var isolatedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<FleetOps.Infrastructure.Persistence.FleetOpsDbContext>));

                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<FleetOps.Infrastructure.Persistence.FleetOpsDbContext>((_, options) =>
                {
                    options.UseNpgsql("Host=127.0.0.1;Port=5439;Database=unreachable_fleetops;Username=postgres;Password=secret_password_123;Timeout=1;CommandTimeout=1;");
                });
            });
        });

        using var client = isolatedFactory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unhealthy", content);
        Assert.DoesNotContain("secret_password_123", content);
        Assert.DoesNotContain("5439", content);
        Assert.DoesNotContain("unreachable_fleetops", content);
        Assert.DoesNotContain("Exception", content);
        Assert.DoesNotContain("Npgsql", content);
    }

    [Fact]
    public async Task OpenApiEndpoint_Returns200OK_WithValidJson()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/vehicles", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/drivers", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/deliveries", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/routes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/maintenances", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedJsonBody_Returns400BadRequest_WithSafeProblemDetails()
    {
        var content = new StringContent("{ not-valid-json }", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/vehicles", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rawContent = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", rawContent);
        Assert.DoesNotContain("Stack", rawContent);
        Assert.DoesNotContain("Npgsql", rawContent);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task RegisterVehicle_WithEmptyJsonObject_Returns400BadRequest_WithProblemDetails()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/vehicles", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task RegisterVehicle_WithCompletelyEmptyBody_Returns400BadRequest_WithProblemDetails()
    {
        var content = new StringContent("", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/vehicles", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task InvalidGuidInRoute_Returns404Or400_WithProblemDetails()
    {
        var response = await Client.PostAsync("/api/vehicles/not-a-guid/activate", null);
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
    }
}

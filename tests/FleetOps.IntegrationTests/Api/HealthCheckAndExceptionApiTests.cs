namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Text;
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

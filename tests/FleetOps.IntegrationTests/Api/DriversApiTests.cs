namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Application.DTOs;
using Xunit;

public sealed class DriversApiTests : BaseApiTest
{
    public DriversApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterDriver_WithValidData_Returns201CreatedAndDriverDto()
    {
        var request = new RegisterDriverRequest(
            FullName: "Jane Driver",
            LicenseNumber: "DL-12345",
            Email: "jane@fleetops.com",
            PhoneNumber: "+5511999998888");

        var response = await Client.PostAsJsonAsync("/api/drivers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Jane Driver", dto.FullName);
        Assert.Equal("DL-12345", dto.LicenseNumber);
        Assert.Equal("Active", dto.Status);
    }

    [Fact]
    public async Task RegisterDriver_WithDuplicateLicense_Returns409Conflict()
    {
        var request = new RegisterDriverRequest("Driver One", "DL-DUP-999", "d1@fleetops.com", "+1111111");
        var first = await Client.PostAsJsonAsync("/api/drivers", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await Client.PostAsJsonAsync("/api/drivers", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var problem = await ReadProblemDetailsAsync(duplicate);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("already registered", problem.Detail);
    }

    [Fact]
    public async Task DeactivateDriver_WhenActive_Returns200OK()
    {
        var driver = await RegisterDriverAsync("DL-DEA-001");

        var response = await Client.PostAsync($"/api/drivers/{driver.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(dto);
        Assert.Equal("Inactive", dto.Status);
    }

    [Fact]
    public async Task ActivateDriver_WhenInactive_Returns200OK()
    {
        var driver = await RegisterDriverAsync("DL-ACT-001");
        await Client.PostAsync($"/api/drivers/{driver.Id}/deactivate", null);

        var response = await Client.PostAsync($"/api/drivers/{driver.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(dto);
        Assert.Equal("Active", dto.Status);
    }

    [Fact]
    public async Task SuspendDriver_WithReason_Returns200OK()
    {
        var driver = await RegisterDriverAsync("DL-SUS-001");

        var request = new SuspendDriverRequest("Traffic violation investigation");
        var response = await Client.PostAsJsonAsync($"/api/drivers/{driver.Id}/suspend", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(dto);
        Assert.Equal("Suspended", dto.Status);
    }

    [Fact]
    public async Task SuspendDriver_WithNonExistentId_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await Client.PostAsJsonAsync($"/api/drivers/{nonExistentId}/suspend", new SuspendDriverRequest("Reason"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    private async Task<DriverDto> RegisterDriverAsync(string licenseNumber)
    {
        var request = new RegisterDriverRequest("Bob Driver", licenseNumber, "bob@fleetops.com", "+12345");
        var response = await Client.PostAsJsonAsync("/api/drivers", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        return dto!;
    }
}

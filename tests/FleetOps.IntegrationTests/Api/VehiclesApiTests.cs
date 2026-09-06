namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using Xunit;

public sealed class VehiclesApiTests : BaseApiTest
{
    public VehiclesApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterVehicle_WithValidData_Returns201CreatedAndVehicleDto()
    {
        var request = new RegisterVehicleRequest(
            LicensePlate: "ABC1D23",
            Type: "Van",
            Make: "Ford",
            Model: "Transit",
            Year: 2023,
            Mileage: 10000,
            CapacityKg: 1500.5m);

        var response = await Client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("ABC1D23", dto.LicensePlate);
        Assert.Equal("Van", dto.Type);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(1500.5m, dto.CapacityKg);
    }

    [Fact]
    public async Task RegisterVehicle_WithDuplicateLicensePlate_Returns409Conflict()
    {
        var request = new RegisterVehicleRequest(
            LicensePlate: "ABC1D23",
            Type: "Van",
            Make: "Ford",
            Model: "Transit",
            Year: 2023,
            Mileage: 10000,
            CapacityKg: 1500m);

        var firstResponse = await Client.PostAsJsonAsync("/api/vehicles", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await Client.PostAsJsonAsync("/api/vehicles", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var problem = await ReadProblemDetailsAsync(duplicateResponse);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("already registered", problem.Detail);
    }

    [Fact]
    public async Task RegisterVehicle_WithInvalidType_Returns400BadRequest()
    {
        var request = new RegisterVehicleRequest(
            LicensePlate: "XYZ9999",
            Type: "Spaceship",
            Make: "NASA",
            Model: "Apollo",
            Year: 2023,
            Mileage: 0,
            CapacityKg: 5000m);

        var response = await Client.PostAsJsonAsync("/api/vehicles", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task ActivateVehicle_WhenAlreadyActive_Returns409Conflict()
    {
        var vehicle = await RegisterVehicleAsync("ACT1111");

        var response = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task DeactivateVehicle_WhenActive_Returns200OK()
    {
        var vehicle = await RegisterVehicleAsync("DEA1111");

        var response = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(dto);
        Assert.Equal("Inactive", dto.Status);
    }

    [Fact]
    public async Task AssignDriver_WithActiveDriver_Returns200OK()
    {
        var vehicle = await RegisterVehicleAsync("ASG1111");
        var driver = await RegisterDriverAsync("DRV001");

        var request = new AssignDriverRequest(driver.Id);
        var response = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/assign-driver", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(dto);
        Assert.Equal(driver.Id, dto.CurrentDriverId);
    }

    [Fact]
    public async Task UnassignDriver_WhenAssigned_Returns200OK()
    {
        var vehicle = await RegisterVehicleAsync("UNS1111");
        var driver = await RegisterDriverAsync("DRV002");

        await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/assign-driver", new AssignDriverRequest(driver.Id));

        var response = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/unassign-driver", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(dto);
        Assert.Null(dto.CurrentDriverId);
    }

    [Fact]
    public async Task UpdateMileage_WithHigherValue_Returns200OK()
    {
        var vehicle = await RegisterVehicleAsync("MIL1111", mileage: 10000);

        var response = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/mileage", new UpdateVehicleMileageRequest(15000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(dto);
        Assert.Equal(15000, dto.Mileage);
    }

    [Fact]
    public async Task UpdateMileage_WithLowerValue_Returns400BadRequest()
    {
        var vehicle = await RegisterVehicleAsync("MIL2222", mileage: 10000);

        var response = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/mileage", new UpdateVehicleMileageRequest(5000));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task SendToMaintenance_AndReturnFromMaintenance_WorkflowSucceeds()
    {
        var vehicle = await RegisterVehicleAsync("MNT1111");

        var sendResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/send-to-maintenance", null);
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sentDto = await sendResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(sentDto);
        Assert.Equal("UnderMaintenance", sentDto.Status);

        var returnResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/return-from-maintenance", null);
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        var returnedDto = await returnResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(returnedDto);
        Assert.Equal("Active", returnedDto.Status);
    }

    [Fact]
    public async Task ActivateVehicle_WithNonExistentId_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await Client.PostAsync($"/api/vehicles/{nonExistentId}/activate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    private async Task<VehicleDto> RegisterVehicleAsync(string plate, int mileage = 1000)
    {
        var request = new RegisterVehicleRequest(plate, "Truck", "Volvo", "FH", 2022, mileage, 20000m);
        var response = await Client.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        return dto!;
    }

    private async Task<DriverDto> RegisterDriverAsync(string licenseNumber)
    {
        var request = new RegisterDriverRequest("John Doe", licenseNumber, "john@example.com", "+123456789");
        var response = await Client.PostAsJsonAsync("/api/drivers", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        return dto!;
    }
}

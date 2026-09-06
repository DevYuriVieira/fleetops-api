namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task VehicleLifecycle_FullEndToEnd_Succeeds()
    {
        var regVehicleRequest = new RegisterVehicleRequest(
            LicensePlate: "E2E8888",
            Type: "Truck",
            Make: "Scania",
            Model: "R450",
            Year: 2024,
            Mileage: 50000,
            CapacityKg: 25000m);

        var regResponse = await Client.PostAsJsonAsync("/api/vehicles", regVehicleRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);
        var vehicle = await regResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(vehicle);
        Assert.Equal("Active", vehicle.Status);
        Assert.Equal(50000, vehicle.Mileage);

        var deactResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactResponse.StatusCode);
        var deactDto = await deactResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(deactDto);
        Assert.Equal("Inactive", deactDto.Status);

        var actResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, actResponse.StatusCode);
        var actDto = await actResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(actDto);
        Assert.Equal("Active", actDto.Status);

        var regDriverRequest = new RegisterDriverRequest("Carlos Silva", "DL-E2E-01", "carlos@fleetops.com", "+5511988887777");
        var driverResponse = await Client.PostAsJsonAsync("/api/drivers", regDriverRequest);
        Assert.Equal(HttpStatusCode.Created, driverResponse.StatusCode);
        var driver = await driverResponse.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(driver);
        Assert.Equal("Active", driver.Status);

        var assignResponse = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/assign-driver", new AssignDriverRequest(driver.Id));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assignedDto = await assignResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(assignedDto);
        Assert.Equal(driver.Id, assignedDto.CurrentDriverId);

        var mileageResponse = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/mileage", new UpdateVehicleMileageRequest(52500));
        Assert.Equal(HttpStatusCode.OK, mileageResponse.StatusCode);
        var mileageDto = await mileageResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(mileageDto);
        Assert.Equal(52500, mileageDto.Mileage);

        var schedRequest = new FleetOps.Api.Contracts.Maintenance.ScheduleMaintenanceRequest(
            vehicle.Id, "Preventive", "Full engine inspection", DateTimeOffset.UtcNow.AddDays(2));
        var schedResponse = await Client.PostAsJsonAsync("/api/maintenances", schedRequest);
        Assert.Equal(HttpStatusCode.Created, schedResponse.StatusCode);
        var maintenance = await schedResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(maintenance);
        Assert.Equal("Scheduled", maintenance.Status);

        var startMntResponse = await Client.PostAsJsonAsync($"/api/maintenances/{maintenance.Id}/start",
            new FleetOps.Api.Contracts.Maintenance.StartMaintenanceRequest(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, startMntResponse.StatusCode);
        var startedMntDto = await startMntResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(startedMntDto);
        Assert.Equal("InProgress", startedMntDto.Status);

        var compMntResponse = await Client.PostAsJsonAsync($"/api/maintenances/{maintenance.Id}/complete",
            new FleetOps.Api.Contracts.Maintenance.CompleteMaintenanceRequest(1250m, "USD", DateTimeOffset.UtcNow, ReturnVehicleToActive: false));
        Assert.Equal(HttpStatusCode.OK, compMntResponse.StatusCode);
        var completedMntDto = await compMntResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(completedMntDto);
        Assert.Equal("Completed", completedMntDto.Status);
        Assert.Equal(1250m, completedMntDto.CostAmount);

        var returnResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/return-from-maintenance", null);
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        var returnedDto = await returnResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(returnedDto);
        Assert.Equal("Active", returnedDto.Status);

        var reassignResponse = await Client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/assign-driver", new AssignDriverRequest(driver.Id));
        Assert.Equal(HttpStatusCode.OK, reassignResponse.StatusCode);

        var unassignResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/unassign-driver", null);
        Assert.Equal(HttpStatusCode.OK, unassignResponse.StatusCode);
        var unassignedDto = await unassignResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(unassignedDto);
        Assert.Null(unassignedDto.CurrentDriverId);

        var finalDeactResponse = await Client.PostAsync($"/api/vehicles/{vehicle.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, finalDeactResponse.StatusCode);
        var finalDto = await finalDeactResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(finalDto);
        Assert.Equal("Inactive", finalDto.Status);

        using var scope = Factory.Services.CreateScope();
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<FleetOps.Infrastructure.Persistence.FleetOpsDbContext>(scope.ServiceProvider);
        var persistedVehicle = await db.Vehicles.FindAsync(vehicle.Id);
        Assert.NotNull(persistedVehicle);
        Assert.Equal(FleetOps.Domain.Enums.VehicleStatus.Inactive, persistedVehicle.Status);
        Assert.Equal(52500, persistedVehicle.Mileage);
        Assert.Null(persistedVehicle.CurrentDriverId);

        var persistedMnt = await db.Maintenances.FindAsync(maintenance.Id);
        Assert.NotNull(persistedMnt);
        Assert.Equal(FleetOps.Domain.Enums.MaintenanceStatus.Completed, persistedMnt.Status);
        Assert.Equal(1250m, persistedMnt.Cost?.Amount);
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

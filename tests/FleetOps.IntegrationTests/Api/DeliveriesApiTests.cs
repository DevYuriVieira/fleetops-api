namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Deliveries;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using FleetOps.Domain.Enums;
using FleetOps.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class DeliveriesApiTests : BaseApiTest
{
    public DeliveriesApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateDelivery_WithValidData_Returns201CreatedAndDeliveryDto()
    {
        var request = new CreateDeliveryRequest(
            TrackingCode: "TRK-000001",
            Origin: new AddressDto("Main St", "100", "Downtown", "New York", "NY", "10001", "USA"),
            Destination: new AddressDto("Broadway", "200", "Midtown", "New York", "NY", "10002", "USA"),
            Priority: "Standard",
            WeightKg: 50.5m);

        var response = await Client.PostAsJsonAsync("/api/deliveries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("TRK-000001", dto.TrackingCode);
        Assert.Equal("Pending", dto.Status);
        Assert.Equal(50.5m, dto.WeightKg);
    }

    [Fact]
    public async Task CreateDelivery_WithDuplicateTrackingCode_Returns409Conflict()
    {
        var request = new CreateDeliveryRequest(
            TrackingCode: "TRK-DUP-001",
            Origin: new AddressDto("Main St", "100", "Downtown", "New York", "NY", "10001", "USA"),
            Destination: new AddressDto("Broadway", "200", "Midtown", "New York", "NY", "10002", "USA"),
            Priority: "Urgent",
            WeightKg: 10m);

        var first = await Client.PostAsJsonAsync("/api/deliveries", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await Client.PostAsJsonAsync("/api/deliveries", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var problem = await ReadProblemDetailsAsync(duplicate);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("already exists", problem.Detail);
    }

    [Fact]
    public async Task AssignDelivery_WithValidVehicleAndDriver_Returns200OK()
    {
        var delivery = await CreateDeliveryAsync("TRK-ASG-001", 100m);
        var vehicle = await RegisterVehicleAsync("DEL-VEH-1", capacityKg: 1000m);
        var driver = await RegisterDriverAsync("DEL-DRV-1");

        var request = new AssignDeliveryRequest(vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddDays(1));
        var response = await Client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/assign", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(dto);
        Assert.Equal("Assigned", dto.Status);
        Assert.Equal(vehicle.Id, dto.AssignedVehicleId);
        Assert.Equal(driver.Id, dto.AssignedDriverId);
    }

    [Fact]
    public async Task AssignDelivery_WhenVehicleCapacityInsufficient_Returns409Conflict()
    {
        var delivery = await CreateDeliveryAsync("TRK-CAP-001", weightKg: 2000m);
        var vehicle = await RegisterVehicleAsync("SML-VEH-1", capacityKg: 500m);
        var driver = await RegisterDriverAsync("CAP-DRV-1");

        var request = new AssignDeliveryRequest(vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddDays(1));
        var response = await Client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/assign", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("insufficient", problem.Detail);
    }

    [Fact]
    public async Task StartAndCompleteDelivery_WorkflowSucceeds()
    {
        var delivery = await CreateDeliveryAsync("TRK-WFL-001", 100m);
        var vehicle = await RegisterVehicleAsync("WFL-VEH-1", 1000m);
        var driver = await RegisterDriverAsync("WFL-DRV-1");

        await Client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/assign",
            new AssignDeliveryRequest(vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddDays(1)));

        var startResponse = await Client.PostAsync($"/api/deliveries/{delivery.Id}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startedDto = await startResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(startedDto);
        Assert.Equal("InTransit", startedDto.Status);

        var completeResponse = await Client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/complete",
            new CompleteDeliveryRequest(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completedDto = await completeResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(completedDto);
        Assert.Equal("Delivered", completedDto.Status);
    }

    [Fact]
    public async Task CancelDelivery_WithReason_Returns200OK()
    {
        var delivery = await CreateDeliveryAsync("TRK-CNC-001", 50m);

        var response = await Client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/cancel",
            new CancelDeliveryRequest("Customer cancelled order"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(dto);
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Customer cancelled order", dto.CancellationReason);
    }

    [Fact]
    public async Task DeliveryLifecycle_FullEndToEnd_Succeeds()
    {
        var trackingCode = $"E2E-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var origin = new AddressDto("100 Market St", "1", "Financial District", "San Francisco", "CA", "94105", "USA");
        var destination = new AddressDto("500 Howard St", "2", "SOMA", "San Francisco", "CA", "94105", "USA");
        var createRequest = new CreateDeliveryRequest(
            TrackingCode: trackingCode,
            Origin: origin,
            Destination: destination,
            Priority: "Urgent",
            WeightKg: 120m);

        var createResponse = await Client.PostAsJsonAsync("/api/deliveries", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var createdDto = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(createdDto);
        Assert.NotEqual(Guid.Empty, createdDto.Id);
        Assert.Equal(trackingCode, createdDto.TrackingCode);
        Assert.Equal("Pending", createdDto.Status);
        Assert.Equal("Urgent", createdDto.Priority);
        Assert.Equal(120m, createdDto.WeightKg);
        Assert.Null(createdDto.AssignedVehicleId);
        Assert.Null(createdDto.AssignedDriverId);
        Assert.Null(createdDto.ActualDeliveryTime);

        var vehiclePlate = $"DLV{Guid.NewGuid():N}"[..7].ToUpperInvariant();
        var regVehicleRequest = new RegisterVehicleRequest(
            LicensePlate: vehiclePlate,
            Type: "Van",
            Make: "Ford",
            Model: "Transit",
            Year: 2023,
            Mileage: 10000,
            CapacityKg: 1500m);
        var regVehicleResponse = await Client.PostAsJsonAsync("/api/vehicles", regVehicleRequest);
        Assert.Equal(HttpStatusCode.Created, regVehicleResponse.StatusCode);
        var vehicleDto = await regVehicleResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(vehicleDto);
        Assert.Equal("Active", vehicleDto.Status);

        var licenseNumber = $"DL-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var regDriverRequest = new RegisterDriverRequest(
            FullName: "Maria Santos",
            LicenseNumber: licenseNumber,
            Email: $"{licenseNumber.ToLowerInvariant()}@fleetops.com",
            PhoneNumber: "+14155552671");
        var regDriverResponse = await Client.PostAsJsonAsync("/api/drivers", regDriverRequest);
        Assert.Equal(HttpStatusCode.Created, regDriverResponse.StatusCode);
        var driverDto = await regDriverResponse.Content.ReadFromJsonAsync<DriverDto>();
        Assert.NotNull(driverDto);
        Assert.Equal("Active", driverDto.Status);

        var estimatedDeliveryTime = new DateTimeOffset(2026, 9, 6, 18, 0, 0, TimeSpan.Zero);
        var assignRequest = new AssignDeliveryRequest(vehicleDto.Id, driverDto.Id, estimatedDeliveryTime);
        var assignResponse = await Client.PostAsJsonAsync($"/api/deliveries/{createdDto.Id}/assign", assignRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assignedDto = await assignResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(assignedDto);
        Assert.Equal("Assigned", assignedDto.Status);
        Assert.Equal(vehicleDto.Id, assignedDto.AssignedVehicleId);
        Assert.Equal(driverDto.Id, assignedDto.AssignedDriverId);
        Assert.Equal(estimatedDeliveryTime, assignedDto.EstimatedDeliveryTime);

        var startResponse = await Client.PostAsync($"/api/deliveries/{createdDto.Id}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startedDto = await startResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(startedDto);
        Assert.Equal("InTransit", startedDto.Status);
        Assert.Equal(vehicleDto.Id, startedDto.AssignedVehicleId);
        Assert.Equal(driverDto.Id, startedDto.AssignedDriverId);

        var actualDeliveryTime = new DateTimeOffset(2026, 9, 6, 14, 30, 0, TimeSpan.Zero);
        var completeRequest = new CompleteDeliveryRequest(actualDeliveryTime);
        var completeResponse = await Client.PostAsJsonAsync($"/api/deliveries/{createdDto.Id}/complete", completeRequest);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completedDto = await completeResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        Assert.NotNull(completedDto);
        Assert.Equal("Delivered", completedDto.Status);
        Assert.Equal(actualDeliveryTime, completedDto.ActualDeliveryTime);
        Assert.Equal(vehicleDto.Id, completedDto.AssignedVehicleId);
        Assert.Equal(driverDto.Id, completedDto.AssignedDriverId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var persistedDelivery = await db.Deliveries.FindAsync(createdDto.Id);
        Assert.NotNull(persistedDelivery);
        Assert.Equal(DeliveryStatus.Delivered, persistedDelivery.Status);
        Assert.Equal(actualDeliveryTime, persistedDelivery.ActualDeliveryTime);
        Assert.Equal(vehicleDto.Id, persistedDelivery.AssignedVehicleId);
        Assert.Equal(driverDto.Id, persistedDelivery.AssignedDriverId);
        Assert.Equal(DeliveryPriority.Urgent, persistedDelivery.Priority);
        Assert.Equal(120m, persistedDelivery.WeightKg);
    }

    private async Task<DeliveryDto> CreateDeliveryAsync(string trackingCode, decimal weightKg)
    {
        var request = new CreateDeliveryRequest(
            trackingCode,
            new AddressDto("123 Street", "1", "Center", "City", "ST", "12345", "Country"),
            new AddressDto("456 Avenue", "2", "Center", "City", "ST", "12345", "Country"),
            "Standard",
            weightKg);

        var response = await Client.PostAsJsonAsync("/api/deliveries", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        return dto!;
    }

    private async Task<VehicleDto> RegisterVehicleAsync(string plate, decimal capacityKg)
    {
        var request = new RegisterVehicleRequest(plate, "Van", "Make", "Model", 2023, 1000, capacityKg);
        var response = await Client.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        return dto!;
    }

    private async Task<DriverDto> RegisterDriverAsync(string license)
    {
        var request = new RegisterDriverRequest("Driver Name", license, $"{license}@test.com", "+12345");
        var response = await Client.PostAsJsonAsync("/api/drivers", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        return dto!;
    }
}

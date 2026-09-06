namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Deliveries;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
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

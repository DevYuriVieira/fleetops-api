namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Deliveries;
using FleetOps.Api.Contracts.Drivers;
using FleetOps.Api.Contracts.Routes;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using Xunit;

public sealed class RoutesApiTests : BaseApiTest
{
    public RoutesApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateRoute_WithValidData_Returns201CreatedAndRouteDto()
    {
        var request = new CreateRouteRequest(
            Origin: new AddressDto("Depot A", "1", "Industrial", "City", "ST", "11111", "Country"),
            Destination: new AddressDto("Depot B", "2", "Commercial", "City", "ST", "22222", "Country"),
            PlannedDeparture: DateTimeOffset.UtcNow.AddHours(2),
            EstimatedArrival: DateTimeOffset.UtcNow.AddHours(6));

        var response = await Client.PostAsJsonAsync("/api/routes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Planned", dto.Status);
    }

    [Fact]
    public async Task AssignRoute_WithValidVehicleAndDriver_Returns200OK()
    {
        var route = await CreateRouteAsync();
        var vehicle = await RegisterVehicleAsync("RTE-VEH-1");
        var driver = await RegisterDriverAsync("RTE-DRV-1");

        var request = new AssignRouteRequest(vehicle.Id, driver.Id);
        var response = await Client.PostAsJsonAsync($"/api/routes/{route.Id}/assign", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(dto);
        Assert.Equal(vehicle.Id, dto.AssignedVehicleId);
        Assert.Equal(driver.Id, dto.AssignedDriverId);
    }

    [Fact]
    public async Task AddAndRemoveDeliveryFromRoute_Succeeds()
    {
        var route = await CreateRouteAsync();
        var delivery = await CreateDeliveryAsync("TRK-RTE-001");

        var addResponse = await Client.PostAsJsonAsync(
            $"/api/routes/{route.Id}/deliveries",
            new AddDeliveryToRouteRequest(delivery.Id));

        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var dtoWithDelivery = await addResponse.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(dtoWithDelivery);
        Assert.Contains(delivery.Id, dtoWithDelivery.DeliveryIds);

        var removeResponse = await Client.DeleteAsync($"/api/routes/{route.Id}/deliveries/{delivery.Id}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        var dtoWithoutDelivery = await removeResponse.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(dtoWithoutDelivery);
        Assert.DoesNotContain(delivery.Id, dtoWithoutDelivery.DeliveryIds);
    }

    [Fact]
    public async Task StartAndCompleteRoute_WorkflowSucceeds()
    {
        var route = await CreateRouteAsync();
        var vehicle = await RegisterVehicleAsync("RTE-STR-1");
        var driver = await RegisterDriverAsync("RTE-STR-D1");
        var delivery = await CreateDeliveryAsync("TRK-STR-001");

        await Client.PostAsJsonAsync($"/api/routes/{route.Id}/assign", new AssignRouteRequest(vehicle.Id, driver.Id));
        await Client.PostAsJsonAsync($"/api/routes/{route.Id}/deliveries", new AddDeliveryToRouteRequest(delivery.Id));

        var startResponse = await Client.PostAsJsonAsync(
            $"/api/routes/{route.Id}/start",
            new StartRouteRequest(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startedDto = await startResponse.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(startedDto);
        Assert.Equal("InProgress", startedDto.Status);

        var completeResponse = await Client.PostAsJsonAsync(
            $"/api/routes/{route.Id}/complete",
            new CompleteRouteRequest(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completedDto = await completeResponse.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(completedDto);
        Assert.Equal("Completed", completedDto.Status);
    }

    [Fact]
    public async Task CancelRoute_WithReason_Returns200OK()
    {
        var route = await CreateRouteAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/routes/{route.Id}/cancel",
            new CancelRouteRequest("Severe weather warning"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RouteDto>();
        Assert.NotNull(dto);
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Severe weather warning", dto.CancellationReason);
    }

    private async Task<RouteDto> CreateRouteAsync()
    {
        var request = new CreateRouteRequest(
            Origin: new AddressDto("Depot A", "1", "Industrial", "City", "ST", "11111", "Country"),
            Destination: new AddressDto("Depot B", "2", "Commercial", "City", "ST", "22222", "Country"),
            PlannedDeparture: DateTimeOffset.UtcNow.AddHours(1),
            EstimatedArrival: DateTimeOffset.UtcNow.AddHours(5));

        var response = await Client.PostAsJsonAsync("/api/routes", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<RouteDto>();
        return dto!;
    }

    private async Task<DeliveryDto> CreateDeliveryAsync(string trackingCode)
    {
        var request = new CreateDeliveryRequest(
            trackingCode,
            new AddressDto("123 Street", "1", "Center", "City", "ST", "12345", "Country"),
            new AddressDto("456 Avenue", "2", "Center", "City", "ST", "12345", "Country"),
            "Standard",
            50m);

        var response = await Client.PostAsJsonAsync("/api/deliveries", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        return dto!;
    }

    private async Task<VehicleDto> RegisterVehicleAsync(string plate)
    {
        var request = new RegisterVehicleRequest(plate, "Truck", "Volvo", "FH", 2023, 1000, 20000m);
        var response = await Client.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        return dto!;
    }

    private async Task<DriverDto> RegisterDriverAsync(string license)
    {
        var request = new RegisterDriverRequest("Route Driver", license, $"{license}@test.com", "+12345");
        var response = await Client.PostAsJsonAsync("/api/drivers", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<DriverDto>();
        return dto!;
    }
}

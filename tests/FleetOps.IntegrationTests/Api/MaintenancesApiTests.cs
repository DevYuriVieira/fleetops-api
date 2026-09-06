namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Contracts.Maintenance;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using Xunit;

public sealed class MaintenancesApiTests : BaseApiTest
{
    public MaintenancesApiTests(FleetOpsApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ScheduleMaintenance_WithValidData_Returns201CreatedAndDto()
    {
        var vehicle = await RegisterVehicleAsync("MNT-TST-1");

        var request = new ScheduleMaintenanceRequest(
            VehicleId: vehicle.Id,
            Type: "Preventive",
            Description: "Oil change and tire rotation",
            ScheduledAt: DateTimeOffset.UtcNow.AddDays(3));

        var response = await Client.PostAsJsonAsync("/api/maintenances", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(vehicle.Id, dto.VehicleId);
        Assert.Equal("Preventive", dto.Type);
        Assert.Equal("Scheduled", dto.Status);
    }

    [Fact]
    public async Task ScheduleMaintenance_WhenVehicleAlreadyHasActiveMaintenance_Returns409Conflict()
    {
        var vehicle = await RegisterVehicleAsync("MNT-DUP-1");

        var request = new ScheduleMaintenanceRequest(
            VehicleId: vehicle.Id,
            Type: "Corrective",
            Description: "Brake pads replacement",
            ScheduledAt: DateTimeOffset.UtcNow.AddDays(1));

        var first = await Client.PostAsJsonAsync("/api/maintenances", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await Client.PostAsJsonAsync("/api/maintenances", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var problem = await ReadProblemDetailsAsync(duplicate);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("active maintenance", problem.Detail);
    }

    [Fact]
    public async Task StartAndCompleteMaintenance_WorkflowSucceeds()
    {
        var vehicle = await RegisterVehicleAsync("MNT-WFL-1");

        var scheduleRequest = new ScheduleMaintenanceRequest(
            vehicle.Id,
            "Preventive",
            "General inspection",
            DateTimeOffset.UtcNow.AddDays(1));

        var scheduleResponse = await Client.PostAsJsonAsync("/api/maintenances", scheduleRequest);
        scheduleResponse.EnsureSuccessStatusCode();
        var maintenance = await scheduleResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(maintenance);

        var startResponse = await Client.PostAsJsonAsync(
            $"/api/maintenances/{maintenance.Id}/start",
            new StartMaintenanceRequest(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startedDto = await startResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(startedDto);
        Assert.Equal("InProgress", startedDto.Status);

        var completeResponse = await Client.PostAsJsonAsync(
            $"/api/maintenances/{maintenance.Id}/complete",
            new CompleteMaintenanceRequest(CostAmount: 350.00m, CostCurrency: "USD", ReturnVehicleToActive: true));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completedDto = await completeResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(completedDto);
        Assert.Equal("Completed", completedDto.Status);
        Assert.Equal(350.00m, completedDto.CostAmount);
    }

    [Fact]
    public async Task CancelMaintenance_WithReason_Returns200OK()
    {
        var vehicle = await RegisterVehicleAsync("MNT-CNC-1");

        var scheduleResponse = await Client.PostAsJsonAsync("/api/maintenances",
            new ScheduleMaintenanceRequest(vehicle.Id, "Preventive", "Checkup", DateTimeOffset.UtcNow.AddDays(2)));
        scheduleResponse.EnsureSuccessStatusCode();
        var maintenance = await scheduleResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(maintenance);

        var cancelResponse = await Client.PostAsJsonAsync(
            $"/api/maintenances/{maintenance.Id}/cancel",
            new CancelMaintenanceRequest("Partially cancelled by fleet manager", ReturnVehicleToActive: false));

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var dto = await cancelResponse.Content.ReadFromJsonAsync<MaintenanceDto>();
        Assert.NotNull(dto);
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Partially cancelled by fleet manager", dto.CancellationReason);
    }

    private async Task<VehicleDto> RegisterVehicleAsync(string plate)
    {
        var request = new RegisterVehicleRequest(plate, "Van", "Ford", "Transit", 2023, 1000, 1500m);
        var response = await Client.PostAsJsonAsync("/api/vehicles", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<VehicleDto>();
        return dto!;
    }
}

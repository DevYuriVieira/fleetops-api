namespace FleetOps.Api.Controllers;

using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.DTOs;
using FleetOps.Application.UseCases.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class VehiclesController : ControllerBase
{
    private readonly RegisterVehicleUseCase _registerVehicleUseCase;
    private readonly ActivateVehicleUseCase _activateVehicleUseCase;
    private readonly DeactivateVehicleUseCase _deactivateVehicleUseCase;
    private readonly AssignDriverToVehicleUseCase _assignDriverToVehicleUseCase;
    private readonly UnassignDriverFromVehicleUseCase _unassignDriverFromVehicleUseCase;
    private readonly UpdateVehicleMileageUseCase _updateVehicleMileageUseCase;
    private readonly SendVehicleToMaintenanceUseCase _sendVehicleToMaintenanceUseCase;
    private readonly ReturnVehicleFromMaintenanceUseCase _returnVehicleFromMaintenanceUseCase;

    public VehiclesController(
        RegisterVehicleUseCase registerVehicleUseCase,
        ActivateVehicleUseCase activateVehicleUseCase,
        DeactivateVehicleUseCase deactivateVehicleUseCase,
        AssignDriverToVehicleUseCase assignDriverToVehicleUseCase,
        UnassignDriverFromVehicleUseCase unassignDriverFromVehicleUseCase,
        UpdateVehicleMileageUseCase updateVehicleMileageUseCase,
        SendVehicleToMaintenanceUseCase sendVehicleToMaintenanceUseCase,
        ReturnVehicleFromMaintenanceUseCase returnVehicleFromMaintenanceUseCase)
    {
        _registerVehicleUseCase = registerVehicleUseCase;
        _activateVehicleUseCase = activateVehicleUseCase;
        _deactivateVehicleUseCase = deactivateVehicleUseCase;
        _assignDriverToVehicleUseCase = assignDriverToVehicleUseCase;
        _unassignDriverFromVehicleUseCase = unassignDriverFromVehicleUseCase;
        _updateVehicleMileageUseCase = updateVehicleMileageUseCase;
        _sendVehicleToMaintenanceUseCase = sendVehicleToMaintenanceUseCase;
        _returnVehicleFromMaintenanceUseCase = returnVehicleFromMaintenanceUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> Register(
        [FromBody] RegisterVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterVehicleCommand(
            request.LicensePlate,
            request.Type,
            request.Make,
            request.Model,
            request.Year,
            request.Mileage,
            request.CapacityKg);

        var result = await _registerVehicleUseCase.ExecuteAsync(command, cancellationToken);

        return Created($"/api/vehicles/{result.Id}", result);
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateVehicleCommand(id);
        var result = await _activateVehicleUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateVehicleCommand(id);
        var result = await _deactivateVehicleUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-driver")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> AssignDriver(
        [FromRoute] Guid id,
        [FromBody] AssignDriverRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignDriverToVehicleCommand(id, request.DriverId);
        var result = await _assignDriverToVehicleUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/unassign-driver")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> UnassignDriver(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new UnassignDriverFromVehicleCommand(id);
        var result = await _unassignDriverFromVehicleUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mileage")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDto>> UpdateMileage(
        [FromRoute] Guid id,
        [FromBody] UpdateVehicleMileageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleMileageCommand(id, request.Mileage);
        var result = await _updateVehicleMileageUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/send-to-maintenance")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> SendToMaintenance(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new SendVehicleToMaintenanceCommand(id);
        var result = await _sendVehicleToMaintenanceUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/return-from-maintenance")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> ReturnFromMaintenance(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ReturnVehicleFromMaintenanceCommand(id);
        var result = await _returnVehicleFromMaintenanceUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

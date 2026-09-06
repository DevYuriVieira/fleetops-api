namespace FleetOps.Api.Controllers;

using FleetOps.Api.Contracts.Maintenance;
using FleetOps.Application.DTOs;
using FleetOps.Application.UseCases.Maintenance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class MaintenancesController : ControllerBase
{
    private readonly ScheduleMaintenanceUseCase _scheduleMaintenanceUseCase;
    private readonly StartMaintenanceUseCase _startMaintenanceUseCase;
    private readonly CompleteMaintenanceUseCase _completeMaintenanceUseCase;
    private readonly CancelMaintenanceUseCase _cancelMaintenanceUseCase;

    public MaintenancesController(
        ScheduleMaintenanceUseCase scheduleMaintenanceUseCase,
        StartMaintenanceUseCase startMaintenanceUseCase,
        CompleteMaintenanceUseCase completeMaintenanceUseCase,
        CancelMaintenanceUseCase cancelMaintenanceUseCase)
    {
        _scheduleMaintenanceUseCase = scheduleMaintenanceUseCase;
        _startMaintenanceUseCase = startMaintenanceUseCase;
        _completeMaintenanceUseCase = completeMaintenanceUseCase;
        _cancelMaintenanceUseCase = cancelMaintenanceUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MaintenanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceDto>> Schedule(
        [FromBody] ScheduleMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ScheduleMaintenanceCommand(
            request.VehicleId,
            request.Type,
            request.Description,
            request.ScheduledAt);

        var result = await _scheduleMaintenanceUseCase.ExecuteAsync(command, cancellationToken);

        return Created($"/api/maintenances/{result.Id}", result);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(MaintenanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceDto>> Start(
        [FromRoute] Guid id,
        [FromBody] StartMaintenanceRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new StartMaintenanceCommand(id, request?.StartedAt);
        var result = await _startMaintenanceUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(MaintenanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceDto>> Complete(
        [FromRoute] Guid id,
        [FromBody] CompleteMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CompleteMaintenanceCommand(
            id,
            request.CostAmount,
            request.CostCurrency,
            request.CompletedAt,
            request.ReturnVehicleToActive);

        var result = await _completeMaintenanceUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(MaintenanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceDto>> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelMaintenanceCommand(
            id,
            request.Reason,
            request.ReturnVehicleToActive);

        var result = await _cancelMaintenanceUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

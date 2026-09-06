namespace FleetOps.Api.Controllers;

using FleetOps.Api.Contracts.Deliveries;
using FleetOps.Application.DTOs;
using FleetOps.Application.UseCases.Deliveries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DeliveriesController : ControllerBase
{
    private readonly CreateDeliveryUseCase _createDeliveryUseCase;
    private readonly AssignDeliveryUseCase _assignDeliveryUseCase;
    private readonly StartDeliveryUseCase _startDeliveryUseCase;
    private readonly CompleteDeliveryUseCase _completeDeliveryUseCase;
    private readonly CancelDeliveryUseCase _cancelDeliveryUseCase;

    public DeliveriesController(
        CreateDeliveryUseCase createDeliveryUseCase,
        AssignDeliveryUseCase assignDeliveryUseCase,
        StartDeliveryUseCase startDeliveryUseCase,
        CompleteDeliveryUseCase completeDeliveryUseCase,
        CancelDeliveryUseCase cancelDeliveryUseCase)
    {
        _createDeliveryUseCase = createDeliveryUseCase;
        _assignDeliveryUseCase = assignDeliveryUseCase;
        _startDeliveryUseCase = startDeliveryUseCase;
        _completeDeliveryUseCase = completeDeliveryUseCase;
        _cancelDeliveryUseCase = cancelDeliveryUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryDto>> Create(
        [FromBody] CreateDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDeliveryCommand(
            request.TrackingCode,
            request.Origin,
            request.Destination,
            request.Priority,
            request.WeightKg);

        var result = await _createDeliveryUseCase.ExecuteAsync(command, cancellationToken);

        return Created($"/api/deliveries/{result.Id}", result);
    }

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryDto>> Assign(
        [FromRoute] Guid id,
        [FromBody] AssignDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignDeliveryCommand(
            id,
            request.VehicleId,
            request.DriverId,
            request.EstimatedDeliveryTime);

        var result = await _assignDeliveryUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryDto>> Start(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new StartDeliveryCommand(id);
        var result = await _startDeliveryUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryDto>> Complete(
        [FromRoute] Guid id,
        [FromBody] CompleteDeliveryRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CompleteDeliveryCommand(id, request?.ActualDeliveryTime);
        var result = await _completeDeliveryUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryDto>> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelDeliveryCommand(id, request.Reason);
        var result = await _cancelDeliveryUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

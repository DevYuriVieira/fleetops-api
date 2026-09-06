namespace FleetOps.Api.Controllers;

using FleetOps.Api.Contracts.Routes;
using FleetOps.Application.DTOs;
using FleetOps.Application.UseCases.Routes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class RoutesController : ControllerBase
{
    private readonly CreateRouteUseCase _createRouteUseCase;
    private readonly AssignRouteUseCase _assignRouteUseCase;
    private readonly AddDeliveryToRouteUseCase _addDeliveryToRouteUseCase;
    private readonly RemoveDeliveryFromRouteUseCase _removeDeliveryFromRouteUseCase;
    private readonly StartRouteUseCase _startRouteUseCase;
    private readonly CompleteRouteUseCase _completeRouteUseCase;
    private readonly CancelRouteUseCase _cancelRouteUseCase;

    public RoutesController(
        CreateRouteUseCase createRouteUseCase,
        AssignRouteUseCase assignRouteUseCase,
        AddDeliveryToRouteUseCase addDeliveryToRouteUseCase,
        RemoveDeliveryFromRouteUseCase removeDeliveryFromRouteUseCase,
        StartRouteUseCase startRouteUseCase,
        CompleteRouteUseCase completeRouteUseCase,
        CancelRouteUseCase cancelRouteUseCase)
    {
        _createRouteUseCase = createRouteUseCase;
        _assignRouteUseCase = assignRouteUseCase;
        _addDeliveryToRouteUseCase = addDeliveryToRouteUseCase;
        _removeDeliveryFromRouteUseCase = removeDeliveryFromRouteUseCase;
        _startRouteUseCase = startRouteUseCase;
        _completeRouteUseCase = completeRouteUseCase;
        _cancelRouteUseCase = cancelRouteUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Create(
        [FromBody] CreateRouteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRouteCommand(
            request.Origin,
            request.Destination,
            request.PlannedDeparture,
            request.EstimatedArrival);

        var result = await _createRouteUseCase.ExecuteAsync(command, cancellationToken);

        return Created($"/api/routes/{result.Id}", result);
    }

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Assign(
        [FromRoute] Guid id,
        [FromBody] AssignRouteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignRouteCommand(id, request.VehicleId, request.DriverId);
        var result = await _assignRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deliveries")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> AddDelivery(
        [FromRoute] Guid id,
        [FromBody] AddDeliveryToRouteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddDeliveryToRouteCommand(id, request.DeliveryId);
        var result = await _addDeliveryToRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/deliveries/{deliveryId:guid}")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> RemoveDelivery(
        [FromRoute] Guid id,
        [FromRoute] Guid deliveryId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveDeliveryFromRouteCommand(id, deliveryId);
        var result = await _removeDeliveryFromRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Start(
        [FromRoute] Guid id,
        [FromBody] StartRouteRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new StartRouteCommand(id, request?.ActualDeparture);
        var result = await _startRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Complete(
        [FromRoute] Guid id,
        [FromBody] CompleteRouteRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CompleteRouteCommand(id, request?.ActualArrival);
        var result = await _completeRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelRouteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelRouteCommand(id, request.Reason);
        var result = await _cancelRouteUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

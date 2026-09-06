namespace FleetOps.Api.Controllers;

using FleetOps.Api.Contracts.Drivers;
using FleetOps.Application.DTOs;
using FleetOps.Application.UseCases.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DriversController : ControllerBase
{
    private readonly RegisterDriverUseCase _registerDriverUseCase;
    private readonly ActivateDriverUseCase _activateDriverUseCase;
    private readonly DeactivateDriverUseCase _deactivateDriverUseCase;
    private readonly SuspendDriverUseCase _suspendDriverUseCase;

    public DriversController(
        RegisterDriverUseCase registerDriverUseCase,
        ActivateDriverUseCase activateDriverUseCase,
        DeactivateDriverUseCase deactivateDriverUseCase,
        SuspendDriverUseCase suspendDriverUseCase)
    {
        _registerDriverUseCase = registerDriverUseCase;
        _activateDriverUseCase = activateDriverUseCase;
        _deactivateDriverUseCase = deactivateDriverUseCase;
        _suspendDriverUseCase = suspendDriverUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDto>> Register(
        [FromBody] RegisterDriverRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterDriverCommand(
            request.FullName,
            request.LicenseNumber,
            request.Email,
            request.PhoneNumber);

        var result = await _registerDriverUseCase.ExecuteAsync(command, cancellationToken);

        return Created($"/api/drivers/{result.Id}", result);
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDto>> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateDriverCommand(id);
        var result = await _activateDriverUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDto>> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateDriverCommand(id);
        var result = await _deactivateDriverUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDto>> Suspend(
        [FromRoute] Guid id,
        [FromBody] SuspendDriverRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SuspendDriverCommand(id, request.Reason);
        var result = await _suspendDriverUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

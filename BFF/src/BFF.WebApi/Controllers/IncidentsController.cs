using BFF.Application.Services;
using BFF.Contracts.Incidents;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using BFF.WebApi.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;
    private readonly IValidator<CreateIncidentRequest> _createValidator;
    private readonly IValidator<ConfirmIncidentRequest> _confirmValidator;

    public IncidentsController(
        IIncidentService incidentService,
        IValidator<CreateIncidentRequest> createValidator,
        IValidator<ConfirmIncidentRequest> confirmValidator)
    {
        _incidentService = incidentService;
        _createValidator = createValidator;
        _confirmValidator = confirmValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Incidents, PermissionFlags.Read)]
    public async Task<IActionResult> List([FromQuery] int? objectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _incidentService.ListAsync(objectId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ResourceCodes.Incidents, PermissionFlags.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await _incidentService.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(ResourceCodes.Incidents, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _incidentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission(ResourceCodes.Incidents, PermissionFlags.Update)]
    public async Task<IActionResult> Confirm(
        Guid id, [FromBody] ConfirmIncidentRequest request, [FromServices] ModelDecisionRelay relay,
        CancellationToken ct)
    {
        await _confirmValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _incidentService.ConfirmAsync(id, currentUser.UserId, request, ct);
        // Метка «происшествие было» — модель по ней проверяет свои отклонения (§9.2).
        await relay.ConfirmedAsync(HttpContext, result, ct);
        return Ok(result);
    }
}

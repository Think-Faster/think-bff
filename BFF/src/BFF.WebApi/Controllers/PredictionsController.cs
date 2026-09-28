using BFF.Application.Services;
using BFF.Contracts.Predictions;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using BFF.WebApi.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
public sealed class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;
    private readonly IValidator<CreatePredictionRequest> _createValidator;
    private readonly IValidator<CreatePredictionDecisionRequest> _decisionValidator;
    private readonly IValidator<CreateFactAlertRequest> _createFactAlertValidator;

    public PredictionsController(
        IPredictionService predictionService,
        IValidator<CreatePredictionRequest> createValidator,
        IValidator<CreatePredictionDecisionRequest> decisionValidator,
        IValidator<CreateFactAlertRequest> createFactAlertValidator)
    {
        _predictionService = predictionService;
        _createValidator = createValidator;
        _decisionValidator = decisionValidator;
        _createFactAlertValidator = createFactAlertValidator;
    }

    [HttpGet("predictions")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int? objectId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _predictionService.ListAsync(objectId, status, page, pageSize, ct));

    [HttpGet("predictions/{id:guid}")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await _predictionService.GetAsync(id, ct));

    /// <summary>Intended to be called by the future tf.forecast.results consumer under a service-account
    /// grant; also usable manually while that consumer doesn't exist yet — see docs/DECISIONS.md.</summary>
    [HttpPost("predictions")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreatePredictionRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _predictionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("predictions/{id:guid}/decisions")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Update)]
    public async Task<IActionResult> Decide(
        Guid id, [FromBody] CreatePredictionDecisionRequest request, [FromServices] ModelDecisionRelay relay,
        CancellationToken ct)
    {
        await _decisionValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        // Take заводит заявку (или прикрепляет к request.TaskId) — её id в TaskId ответа.
        var result = await _predictionService.DecideAsync(id, currentUser.UserId, request, ct);
        await relay.DecisionAsync(HttpContext, result, ct);
        return Ok(result);
    }

    [HttpGet("fact-alerts")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Read)]
    public async Task<IActionResult> ListFactAlerts(
        [FromQuery] int? objectId, [FromQuery] bool? live, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _predictionService.ListFactAlertsAsync(objectId, live, page, pageSize, ct));

    [HttpPost("fact-alerts")]
    [RequirePermission(ResourceCodes.Predictions, PermissionFlags.Create)]
    public async Task<IActionResult> CreateFactAlert(
        [FromBody] CreateFactAlertRequest request, [FromServices] FactNotifier notifier, CancellationToken ct)
    {
        await _createFactAlertValidator.ValidateAndThrowAsync(request, ct);
        var result = await _predictionService.CreateFactAlertAsync(request, ct);
        // Тот же путь, что у события модели из Kafka (FactResultsConsumer): кто на смене — письмо и Telegram.
        var requestId = HttpContext.Request.Headers["X-Request-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        await notifier.NotifyAsync(result, new FactContext(RequestId: requestId), ct);
        return CreatedAtAction(nameof(ListFactAlerts), null, result);
    }
}

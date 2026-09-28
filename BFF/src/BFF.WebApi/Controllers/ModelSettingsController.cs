using BFF.Application.Services;
using BFF.Contracts.Common;
using BFF.Contracts.ModelSettings;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using BFF.WebApi.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

/// <summary>D8 admin-settings block: model version control, coefficients, retrain jobs, ignored ranges —
/// bundled under one resource code (model_settings) per docs/DECISIONS.md.</summary>
[ApiController]
public sealed class ModelSettingsController : ControllerBase
{
    private readonly IModelSettingsService _service;
    private readonly IValidator<CreateModelVersionRequest> _createModelVersionValidator;
    private readonly IValidator<CreateCoefficientRequest> _createCoefficientValidator;
    private readonly IValidator<CreateRetrainJobRequest> _createRetrainJobValidator;
    private readonly IValidator<CreateIgnoredRangeRequest> _createIgnoredRangeValidator;
    private readonly IValidator<UpsertWorkScheduleEntryRequest> _upsertWorkValidator;

    public ModelSettingsController(
        IModelSettingsService service,
        IValidator<CreateModelVersionRequest> createModelVersionValidator,
        IValidator<CreateCoefficientRequest> createCoefficientValidator,
        IValidator<CreateRetrainJobRequest> createRetrainJobValidator,
        IValidator<CreateIgnoredRangeRequest> createIgnoredRangeValidator,
        IValidator<UpsertWorkScheduleEntryRequest> upsertWorkValidator)
    {
        _service = service;
        _createModelVersionValidator = createModelVersionValidator;
        _createCoefficientValidator = createCoefficientValidator;
        _createRetrainJobValidator = createRetrainJobValidator;
        _createIgnoredRangeValidator = createIgnoredRangeValidator;
        _upsertWorkValidator = upsertWorkValidator;
    }

    [HttpGet("model-versions")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Read)]
    public async Task<IActionResult> ListModelVersions(CancellationToken ct)
        => Ok(await _service.ListModelVersionsAsync(ct));

    [HttpPost("model-versions")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateModelVersion([FromBody] CreateModelVersionRequest request, CancellationToken ct)
    {
        await _createModelVersionValidator.ValidateAndThrowAsync(request, ct);
        var result = await _service.CreateModelVersionAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("model-versions/{id}/activate")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> ActivateModelVersion(string id, CancellationToken ct)
    {
        var currentUser = HttpContext.GetCurrentUser()!;
        return Ok(await _service.ActivateModelVersionAsync(id, currentUser.UserId, ct));
    }

    [HttpGet("coefficients")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Read)]
    public async Task<IActionResult> ListCoefficients(CancellationToken ct)
        => Ok(await _service.ListCoefficientsAsync(ct));

    [HttpPost("coefficients")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateCoefficient([FromBody] CreateCoefficientRequest request, CancellationToken ct)
    {
        await _createCoefficientValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _service.CreateCoefficientAsync(currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("retrain-jobs")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Read)]
    public async Task<IActionResult> ListRetrainJobs(CancellationToken ct)
        => Ok(await _service.ListRetrainJobsAsync(ct));

    [HttpPost("retrain-jobs")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateRetrainJob([FromBody] CreateRetrainJobRequest request, CancellationToken ct)
    {
        await _createRetrainJobValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _service.CreateRetrainJobAsync(currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("ignored-ranges")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Read)]
    public async Task<IActionResult> ListIgnoredRanges(CancellationToken ct)
        => Ok(await _service.ListIgnoredRangesAsync(ct));

    [HttpPost("ignored-ranges")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateIgnoredRange([FromBody] CreateIgnoredRangeRequest request, CancellationToken ct)
    {
        await _createIgnoredRangeValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _service.CreateIgnoredRangeAsync(currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("ignored-ranges/{id:guid}")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> DeleteIgnoredRange(Guid id, CancellationToken ct)
    {
        await _service.DeleteIgnoredRangeAsync(id, ct);
        return NoContent();
    }

    /// <summary>Planned works schedule (works_2026): the model mutes alarms of the listed incident types on the
    /// object and its descendants for the window. Only the current, non-deleted version of each work is returned.</summary>
    [HttpGet("work-schedule")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Read)]
    public async Task<IActionResult> ListWorkSchedule(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int? objectId, CancellationToken ct)
        => Ok(await _service.ListWorkScheduleAsync(from, to, objectId, ct));

    [HttpPost("work-schedule")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateWork([FromBody] UpsertWorkScheduleEntryRequest request, CancellationToken ct)
    {
        await _upsertWorkValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _service.CreateWorkAsync(currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("work-schedule/{workId:long}")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> UpdateWork(long workId, [FromBody] UpsertWorkScheduleEntryRequest request, CancellationToken ct)
    {
        await _upsertWorkValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        return Ok(await _service.UpdateWorkAsync(workId, currentUser.UserId, request, ct));
    }

    [HttpDelete("work-schedule/{workId:long}")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> DeleteWork(long workId, CancellationToken ct)
    {
        var currentUser = HttpContext.GetCurrentUser()!;
        await _service.DeleteWorkAsync(workId, currentUser.UserId, ct);
        return NoContent();
    }

    // Команды модели (§13.3): состояние — в самой модели (/api/ml/status), BFF только передаёт снимок.
    // 202 — команда в очереди; принят ли снимок, видно по номеру версии в /status и в аудите модели.

    [HttpPost("model-commands/switch")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> SwitchModelVersion(
        [FromBody] SwitchModelVersionRequest request, [FromServices] IValidator<SwitchModelVersionRequest> validator,
        [FromServices] ModelSettingsRelay relay, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResult(await relay.SwitchAsync(HttpContext, request, ct));
    }

    [HttpPost("model-commands/operating")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> SetOperatingSettings(
        [FromBody] OperatingSettingsRequest request, [FromServices] IValidator<OperatingSettingsRequest> validator,
        [FromServices] ModelSettingsRelay relay, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResult(await relay.OperatingAsync(HttpContext, request, ct));
    }

    [HttpPost("model-commands/gaps")]
    [RequirePermission(ResourceCodes.ModelSettings, PermissionFlags.Manage)]
    public async Task<IActionResult> SetIgnoredPeriods(
        [FromBody] IgnoredPeriodsRequest request, [FromServices] IValidator<IgnoredPeriodsRequest> validator,
        [FromServices] ModelSettingsRelay relay, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResult(await relay.GapsAsync(HttpContext, request, ct));
    }

    private IActionResult CommandResult(ModelCommandAcceptedResponse? accepted) => accepted is null
        ? StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
        {
            Code = "model_commands_unavailable",
            Message = "Model command broker is unavailable, retry later.",
        })
        : StatusCode(StatusCodes.Status202Accepted, accepted);
}

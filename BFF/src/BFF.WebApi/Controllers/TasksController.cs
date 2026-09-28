using BFF.Application.Services;
using BFF.Contracts.Tasks;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly IWorkTaskService _taskService;
    private readonly IValidator<CreateWorkTaskRequest> _createValidator;
    private readonly IValidator<UpdateWorkTaskRequest> _updateValidator;
    private readonly IValidator<AttachPredictionRequest> _attachValidator;
    private readonly IValidator<CreateTaskAssignmentRequest> _assignValidator;
    private readonly IValidator<CreateTaskReportRequest> _reportValidator;
    private readonly IValidator<CreateTaskReturnRequest> _returnValidator;
    private readonly IValidator<TaskTransitionRequest> _transitionValidator;

    public TasksController(
        IWorkTaskService taskService,
        IValidator<CreateWorkTaskRequest> createValidator,
        IValidator<UpdateWorkTaskRequest> updateValidator,
        IValidator<AttachPredictionRequest> attachValidator,
        IValidator<CreateTaskAssignmentRequest> assignValidator,
        IValidator<CreateTaskReportRequest> reportValidator,
        IValidator<CreateTaskReturnRequest> returnValidator,
        IValidator<TaskTransitionRequest> transitionValidator)
    {
        _taskService = taskService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _attachValidator = attachValidator;
        _assignValidator = assignValidator;
        _reportValidator = reportValidator;
        _returnValidator = returnValidator;
        _transitionValidator = transitionValidator;
    }

    /// <summary>assignedToMe=true — заявки, где текущий пользователь назначен инженером (экран инженера).</summary>
    [HttpGet]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? dispatcherId, [FromQuery] string? status, [FromQuery] bool assignedToMe = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var engineerId = assignedToMe ? HttpContext.GetCurrentUser()!.UserId : (Guid?)null;
        return Ok(await _taskService.ListAsync(dispatcherId, engineerId, status, page, pageSize, ct));
    }

    /// <summary>Исполнители для назначения (role=engineers) и возврата диспетчеру (role=dispatchers).</summary>
    [HttpGet("assignees")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Read)]
    public async Task<IActionResult> Assignees([FromQuery] string role, CancellationToken ct)
    {
        if (role is not ("engineers" or "dispatchers"))
        {
            throw new ArgumentException("role: engineers или dispatchers.", nameof(role));
        }

        return Ok(await _taskService.ListAssigneesAsync(role, ct));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await _taskService.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateWorkTaskRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _taskService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkTaskRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _taskService.UpdateAsync(id, request, ct));
    }

    /// <summary>"First to take it, owns it" — section 4.1 of the domain doc. Atomic against concurrent
    /// callers; returns 409 task_already_taken if someone beat you to it.</summary>
    [HttpPost("{id:guid}/take")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Take(Guid id, CancellationToken ct)
    {
        var currentUser = HttpContext.GetCurrentUser()!;
        return Ok(await _taskService.TakeAsync(id, currentUser.UserId, ct));
    }

    [HttpPost("{id:guid}/predictions")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> AttachPrediction(Guid id, [FromBody] AttachPredictionRequest request, CancellationToken ct)
    {
        await _attachValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        await _taskService.AttachPredictionAsync(id, currentUser.UserId, request, ct);
        return Created();
    }

    [HttpDelete("{id:guid}/predictions/{predictionId:guid}")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> DetachPrediction(Guid id, Guid predictionId, CancellationToken ct)
    {
        await _taskService.DetachPredictionAsync(id, predictionId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assignments")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] CreateTaskAssignmentRequest request, CancellationToken ct)
    {
        await _assignValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _taskService.AssignAsync(id, currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/reports")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> AddReport(Guid id, [FromBody] CreateTaskReportRequest request, CancellationToken ct)
    {
        await _reportValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _taskService.AddReportAsync(id, currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/returns")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Return(Guid id, [FromBody] CreateTaskReturnRequest request, CancellationToken ct)
    {
        await _returnValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _taskService.ReturnAsync(id, currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Инженер приступил к работе (Assigned -> EngineerWorking). 409 invalid_status — не тот статус.</summary>
    [HttpPost("{id:guid}/start")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Start(Guid id, [FromBody] TaskTransitionRequest? request, CancellationToken ct)
    {
        request ??= new TaskTransitionRequest();
        await _transitionValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _taskService.StartAsync(id, request, ct));
    }

    /// <summary>Отчёт принят, заявка закрыта (Completed -> Closed).</summary>
    [HttpPost("{id:guid}/close")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Close(Guid id, [FromBody] TaskTransitionRequest? request, CancellationToken ct)
    {
        request ??= new TaskTransitionRequest();
        await _transitionValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _taskService.CloseAsync(id, request, ct));
    }

    /// <summary>Отмена заявки из любого активного статуса.</summary>
    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(ResourceCodes.Tasks, PermissionFlags.Update)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] TaskTransitionRequest? request, CancellationToken ct)
    {
        request ??= new TaskTransitionRequest();
        await _transitionValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _taskService.CancelAsync(id, request, ct));
    }
}

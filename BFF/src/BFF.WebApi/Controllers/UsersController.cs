using BFF.Application.Services;
using BFF.Contracts.Engineers;
using BFF.Contracts.Schedule;
using BFF.Contracts.Users;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IScheduleService _scheduleService;
    private readonly IAssignedObjectService _assignedObjectService;
    private readonly IEngineerService _engineerService;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<AddUserGroupsRequest> _addGroupsValidator;
    private readonly IValidator<CreateScheduleEntryRequest> _createScheduleValidator;
    private readonly IValidator<AssignObjectRequest> _assignObjectValidator;
    private readonly IValidator<UpsertEngineerProfileRequest> _upsertEngineerProfileValidator;

    public UsersController(
        IUserService userService,
        IScheduleService scheduleService,
        IAssignedObjectService assignedObjectService,
        IEngineerService engineerService,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<AddUserGroupsRequest> addGroupsValidator,
        IValidator<CreateScheduleEntryRequest> createScheduleValidator,
        IValidator<AssignObjectRequest> assignObjectValidator,
        IValidator<UpsertEngineerProfileRequest> upsertEngineerProfileValidator)
    {
        _userService = userService;
        _scheduleService = scheduleService;
        _assignedObjectService = assignedObjectService;
        _engineerService = engineerService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addGroupsValidator = addGroupsValidator;
        _createScheduleValidator = createScheduleValidator;
        _assignObjectValidator = assignObjectValidator;
        _upsertEngineerProfileValidator = upsertEngineerProfileValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Read)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _userService.ListAsync(search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetAsync(id, ct);
        return Ok(user);
    }

    [HttpPost]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var user = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var user = await _userService.UpdateAsync(id, request, ct);
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Delete)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool soft = true, CancellationToken ct = default)
    {
        await _userService.DeleteAsync(id, soft, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/groups")]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Update)]
    public async Task<IActionResult> AddGroups(Guid id, [FromBody] AddUserGroupsRequest request, CancellationToken ct)
    {
        await _addGroupsValidator.ValidateAndThrowAsync(request, ct);
        var groups = await _userService.AddGroupsAsync(id, request, ct);
        return Ok(groups);
    }

    [HttpDelete("{id:guid}/groups/{groupId:guid}")]
    [RequirePermission(ResourceCodes.Users, PermissionFlags.Update)]
    public async Task<IActionResult> RemoveGroup(Guid id, Guid groupId, CancellationToken ct)
    {
        await _userService.RemoveGroupAsync(id, groupId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/schedule")]
    [RequirePermission(ResourceCodes.Schedule, PermissionFlags.Read)]
    public async Task<IActionResult> ListSchedule(Guid id, CancellationToken ct)
        => Ok(await _scheduleService.ListAsync(id, ct));

    [HttpPost("{id:guid}/schedule")]
    [RequirePermission(ResourceCodes.Schedule, PermissionFlags.Update)]
    public async Task<IActionResult> CreateScheduleEntry(Guid id, [FromBody] CreateScheduleEntryRequest request, CancellationToken ct)
    {
        await _createScheduleValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _scheduleService.CreateAsync(id, currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{id:guid}/schedule/{entryId:guid}")]
    [RequirePermission(ResourceCodes.Schedule, PermissionFlags.Update)]
    public async Task<IActionResult> DeleteScheduleEntry(Guid id, Guid entryId, CancellationToken ct)
    {
        await _scheduleService.DeleteAsync(id, entryId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/assigned-objects")]
    [RequirePermission(ResourceCodes.AssignedObjects, PermissionFlags.Read)]
    public async Task<IActionResult> ListAssignedObjects(Guid id, CancellationToken ct)
        => Ok(await _assignedObjectService.ListAsync(id, ct));

    [HttpPost("{id:guid}/assigned-objects")]
    [RequirePermission(ResourceCodes.AssignedObjects, PermissionFlags.Update)]
    public async Task<IActionResult> AssignObject(Guid id, [FromBody] AssignObjectRequest request, CancellationToken ct)
    {
        await _assignObjectValidator.ValidateAndThrowAsync(request, ct);
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _assignedObjectService.AssignAsync(id, currentUser.UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{id:guid}/assigned-objects/{objectId:int}")]
    [RequirePermission(ResourceCodes.AssignedObjects, PermissionFlags.Update)]
    public async Task<IActionResult> UnassignObject(Guid id, int objectId, CancellationToken ct)
    {
        await _assignedObjectService.UnassignAsync(id, objectId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/engineer-profile")]
    [RequirePermission(ResourceCodes.Engineers, PermissionFlags.Read)]
    public async Task<IActionResult> GetEngineerProfile(Guid id, CancellationToken ct)
    {
        var profile = await _engineerService.GetProfileAsync(id, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("{id:guid}/engineer-profile")]
    [RequirePermission(ResourceCodes.Engineers, PermissionFlags.Update)]
    public async Task<IActionResult> UpsertEngineerProfile(Guid id, [FromBody] UpsertEngineerProfileRequest request, CancellationToken ct)
    {
        await _upsertEngineerProfileValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _engineerService.UpsertProfileAsync(id, request, ct));
    }
}

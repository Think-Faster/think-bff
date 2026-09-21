using BFF.Application.Services;
using BFF.Contracts.Users;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<AddUserGroupsRequest> _addGroupsValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<AddUserGroupsRequest> addGroupsValidator)
    {
        _userService = userService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addGroupsValidator = addGroupsValidator;
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
}

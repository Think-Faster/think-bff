using BFF.Application.Services;
using BFF.Contracts.Groups;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("groups")]
public sealed class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly IValidator<CreateGroupRequest> _createValidator;
    private readonly IValidator<UpdateGroupRequest> _updateValidator;
    private readonly IValidator<AddGroupMemberRequest> _addMemberValidator;
    private readonly IValidator<AddGroupMembersBatchRequest> _addMembersBatchValidator;

    public GroupsController(
        IGroupService groupService,
        IValidator<CreateGroupRequest> createValidator,
        IValidator<UpdateGroupRequest> updateValidator,
        IValidator<AddGroupMemberRequest> addMemberValidator,
        IValidator<AddGroupMembersBatchRequest> addMembersBatchValidator)
    {
        _groupService = groupService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addMemberValidator = addMemberValidator;
        _addMembersBatchValidator = addMembersBatchValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Read)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _groupService.ListAsync(search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var group = await _groupService.GetAsync(id, ct);
        return Ok(group);
    }

    [HttpPost]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var group = await _groupService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var group = await _groupService.UpdateAsync(id, request, ct);
        return Ok(group);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _groupService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Update)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddGroupMemberRequest request, CancellationToken ct)
    {
        await _addMemberValidator.ValidateAndThrowAsync(request, ct);
        await _groupService.AddMemberAsync(id, request, ct);
        return Created();
    }

    [HttpPost("{id:guid}/members/batch")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Update)]
    public async Task<IActionResult> AddMembersBatch(Guid id, [FromBody] AddGroupMembersBatchRequest request, CancellationToken ct)
    {
        await _addMembersBatchValidator.ValidateAndThrowAsync(request, ct);
        await _groupService.AddMembersBatchAsync(id, request, ct);
        return Created();
    }

    [HttpDelete("{id:guid}/members/{memberType}/{memberId:guid}")]
    [RequirePermission(ResourceCodes.Groups, PermissionFlags.Update)]
    public async Task<IActionResult> RemoveMember(Guid id, string memberType, Guid memberId, CancellationToken ct)
    {
        await _groupService.RemoveMemberAsync(id, memberType, memberId, ct);
        return NoContent();
    }
}

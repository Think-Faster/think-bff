using BFF.Application.Services;
using BFF.Contracts.Permissions;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Authorize]
public sealed class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CreateGrantRequest> _createGrantValidator;
    private readonly IValidator<CreateResourceRequest> _createResourceValidator;

    public PermissionsController(
        IPermissionService permissionService,
        IValidator<CreateGrantRequest> createGrantValidator,
        IValidator<CreateResourceRequest> createResourceValidator)
    {
        _permissionService = permissionService;
        _createGrantValidator = createGrantValidator;
        _createResourceValidator = createResourceValidator;
    }

    [HttpGet("permissions/me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var currentUser = HttpContext.GetCurrentUser()!;
        var result = await _permissionService.GetMyPermissionsAsync(currentUser.UserId, ct);
        return Ok(result);
    }

    [HttpGet("permissions/check")]
    public async Task<IActionResult> Check([FromQuery] string resource, [FromQuery] string permission, CancellationToken ct)
    {
        var currentUser = HttpContext.GetCurrentUser()!;
        var allowed = await _permissionService.CheckAsync(currentUser.UserId, resource, permission, ct);
        return Ok(new CheckPermissionResponse { Allowed = allowed });
    }

    [HttpGet("permissions/grants")]
    [RequirePermission(ResourceCodes.Permissions, PermissionFlags.Read)]
    public async Task<IActionResult> GetGrants([FromQuery] string? principalType, [FromQuery] Guid? principalId, CancellationToken ct)
    {
        var grants = await _permissionService.GetGrantsAsync(principalType, principalId, ct);
        return Ok(grants);
    }

    [HttpPost("permissions/grants")]
    [RequirePermission(ResourceCodes.Permissions, PermissionFlags.Manage)]
    public async Task<IActionResult> UpsertGrant([FromBody] CreateGrantRequest request, CancellationToken ct)
    {
        await _createGrantValidator.ValidateAndThrowAsync(request, ct);
        var grant = await _permissionService.UpsertGrantAsync(request, ct);
        return Ok(grant);
    }

    [HttpDelete("permissions/grants/{id:guid}")]
    [RequirePermission(ResourceCodes.Permissions, PermissionFlags.Manage)]
    public async Task<IActionResult> RevokeGrant(Guid id, CancellationToken ct)
    {
        await _permissionService.RevokeGrantAsync(id, ct);
        return NoContent();
    }

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(CancellationToken ct)
    {
        var resources = await _permissionService.GetResourcesAsync(ct);
        return Ok(resources);
    }

    [HttpPost("resources")]
    [RequirePermission(ResourceCodes.Permissions, PermissionFlags.Manage)]
    public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest request, CancellationToken ct)
    {
        await _createResourceValidator.ValidateAndThrowAsync(request, ct);
        var resource = await _permissionService.CreateResourceAsync(request, ct);
        return CreatedAtAction(nameof(GetResources), null, resource);
    }
}

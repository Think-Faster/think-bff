using BFF.Application.Services;
using BFF.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace BFF.WebApi.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationHandler(IPermissionService permissionService, IHttpContextAccessor httpContextAccessor)
    {
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var currentUser = httpContext?.GetCurrentUser();

        if (currentUser is null)
        {
            return;
        }

        var allowed = await _permissionService.HasPermissionAsync(
            currentUser.UserId, requirement.ResourceCode, requirement.Required, httpContext!.RequestAborted);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}

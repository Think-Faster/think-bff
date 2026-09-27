using BFF.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace BFF.WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string resourceCode, PermissionFlags required)
        => Policy = PermissionPolicyProvider.BuildPolicyName(resourceCode, required);
}

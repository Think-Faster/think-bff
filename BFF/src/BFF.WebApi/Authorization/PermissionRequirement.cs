using BFF.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace BFF.WebApi.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string ResourceCode { get; }
    public PermissionFlags Required { get; }

    public PermissionRequirement(string resourceCode, PermissionFlags required)
    {
        ResourceCode = resourceCode;
        Required = required;
    }
}

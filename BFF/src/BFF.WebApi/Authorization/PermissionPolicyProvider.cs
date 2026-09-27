using BFF.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BFF.WebApi.Authorization;

/// <summary>
/// Builds `perm:{resource}:{mask}` authorization policies on demand instead of requiring them to be
/// registered up front (section 9: "Заранее перечислять политики не нужно").
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public static string BuildPolicyName(string resourceCode, PermissionFlags required)
        => $"{PolicyPrefix}{resourceCode}:{(int)required}";

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        if (!TryParsePolicyName(policyName, out var resourceCode, out var required))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(resourceCode, required))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    private static bool TryParsePolicyName(string policyName, out string resourceCode, out PermissionFlags required)
    {
        resourceCode = string.Empty;
        required = PermissionFlags.None;

        var parts = policyName.Substring(PolicyPrefix.Length).Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var mask))
        {
            return false;
        }

        resourceCode = parts[0];
        required = (PermissionFlags)mask;
        return true;
    }
}

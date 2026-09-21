using BFF.Contracts.Permissions;
using BFF.Models.Enums;

namespace BFF.Application.Services;

public interface IPermissionService
{
    Task<MyPermissionsResponse> GetMyPermissionsAsync(Guid userId, CancellationToken ct);

    Task<bool> CheckAsync(Guid userId, string resourceCode, string permission, CancellationToken ct);

    /// <summary>Used by <c>PermissionAuthorizationHandler</c> to evaluate <c>[RequirePermission]</c>.</summary>
    Task<bool> HasPermissionAsync(Guid userId, string resourceCode, PermissionFlags required, CancellationToken ct);

    Task<IReadOnlyList<GrantDto>> GetGrantsAsync(string? principalType, Guid? principalId, CancellationToken ct);

    Task<GrantDto> UpsertGrantAsync(CreateGrantRequest request, CancellationToken ct);

    Task RevokeGrantAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ResourceDto>> GetResourcesAsync(CancellationToken ct);

    Task<ResourceDto> CreateResourceAsync(CreateResourceRequest request, CancellationToken ct);
}

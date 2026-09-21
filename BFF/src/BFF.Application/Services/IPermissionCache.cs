using BFF.Models.Enums;

namespace BFF.Application.Services;

public interface IPermissionCache
{
    /// <summary>Effective permissions of a user, resource code -> mask. Served from memory unless the
    /// polled rbac_version changed or the per-entry TTL expired (section 7.4).</summary>
    Task<IReadOnlyDictionary<string, PermissionFlags>> GetPermissionsAsync(Guid userId, CancellationToken ct);
}

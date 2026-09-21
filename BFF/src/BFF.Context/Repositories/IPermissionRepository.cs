namespace BFF.Context.Repositories;

public interface IPermissionRepository
{
    /// <summary>All effective permissions of a user, aggregated (bit-OR) across the user and every
    /// group they belong to, directly or transitively. One round trip via EF, no per-group queries
    /// (section 7.2).</summary>
    Task<IReadOnlyDictionary<string, int>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);

    Task<long> GetRbacVersionAsync(CancellationToken ct);
}

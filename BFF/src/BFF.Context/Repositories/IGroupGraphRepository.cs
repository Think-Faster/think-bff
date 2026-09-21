namespace BFF.Context.Repositories;

/// <summary>
/// Maintains the group composition graph: mutation locking, cycle detection against the materialized
/// closure, and full recomputation of that closure. All methods must run inside a caller-managed
/// transaction (section 6.3 of the spec).
/// </summary>
public interface IGroupGraphRepository
{
    /// <summary>
    /// Serializes graph mutations across concurrent requests. Implemented as an EF-tracked update of the
    /// single rbac_version row: Postgres holds a row lock on it until commit/rollback, so a second
    /// concurrent call blocks here until the first transaction finishes — the same effect the spec's
    /// pg_advisory_xact_lock would give, achieved with a plain EF SaveChanges instead of raw SQL. It also
    /// performs the cache-version bump that section 7.4 requires for any group_members mutation, so
    /// callers must not increment the version separately for the same transaction.
    /// </summary>
    Task LockGraphAsync(CancellationToken ct);

    /// <summary>
    /// True if adding an edge "parent contains child" would close a cycle: either a direct self-loop,
    /// or parent is already a descendant of child in group_closure.
    /// </summary>
    Task<bool> WouldCreateCycleAsync(Guid parentGroupId, Guid childGroupId, CancellationToken ct);

    /// <summary>Full rebuild of group_closure from group_members, computed in memory from the edge list
    /// and written back with plain EF bulk delete/insert (section 6.3).</summary>
    Task RecomputeClosureAsync(CancellationToken ct);

    /// <summary>Inserts the reflexive row (g, g, 0) for a newly created group, if not already present.</summary>
    Task EnsureReflexiveClosureAsync(Guid groupId, CancellationToken ct);
}

using Microsoft.EntityFrameworkCore;

namespace BFF.Context;

public static class BffDbContextExtensions
{
    /// <summary>Marks the single rbac_version row for increment. Does not call SaveChanges — the caller
    /// is expected to do that once, together with whichever users/groups/group_members/access_grants
    /// change triggered the bump, so both land in the same transaction (section 7.4).</summary>
    public static async Task MarkRbacVersionForIncrementAsync(this BffDbContext context, CancellationToken ct)
    {
        var version = await context.RbacVersions.SingleAsync(v => v.Id == 1, ct);
        version.Value += 1;
    }
}

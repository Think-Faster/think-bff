using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Context.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly BffDbContext _context;

    public PermissionRepository(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        var groupIds = await GetEffectiveGroupIdsAsync(userId, ct);

        var grants = await _context.AccessGrants.AsNoTracking()
            .Where(ag =>
                (ag.PrincipalType == PrincipalType.User && ag.PrincipalId == userId) ||
                (ag.PrincipalType == PrincipalType.Group && groupIds.Contains(ag.PrincipalId)))
            .Select(ag => new { Code = ag.Resource!.Code, ag.PermissionMask })
            .ToListAsync(ct);

        return grants
            .GroupBy(g => g.Code)
            .ToDictionary(
                g => g.Key,
                g => (int)g.Aggregate(PermissionFlags.None, (acc, x) => acc | x.PermissionMask));
    }

    public async Task<long> GetRbacVersionAsync(CancellationToken ct)
    {
        var version = await _context.RbacVersions.AsNoTracking()
            .Where(v => v.Id == 1)
            .Select(v => (long?)v.Value)
            .SingleOrDefaultAsync(ct);

        return version ?? 0;
    }

    /// <summary>Every group the user belongs to, directly or transitively — the LINQ equivalent of the
    /// user_groups CTE in section 6.4, expressed as an EF join instead of raw SQL.</summary>
    private async Task<List<Guid>> GetEffectiveGroupIdsAsync(Guid userId, CancellationToken ct)
    {
        return await (
                from m in _context.GroupMembers
                join c in _context.GroupClosures on m.GroupId equals c.DescendantId
                where m.MemberType == MemberType.User && m.MemberId == userId
                select c.AncestorId)
            .Distinct()
            .ToListAsync(ct);
    }
}

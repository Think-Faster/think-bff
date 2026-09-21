using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Context.Repositories;

public sealed class GroupGraphRepository : IGroupGraphRepository
{
    private const int MaxDepth = 32;

    private readonly BffDbContext _context;

    public GroupGraphRepository(BffDbContext context)
    {
        _context = context;
    }

    public async Task LockGraphAsync(CancellationToken ct)
    {
        await _context.MarkRbacVersionForIncrementAsync(ct);
        // Flushed immediately (not batched with later SaveChanges calls) so the row lock this UPDATE
        // takes in Postgres is held for the rest of the ambient transaction, serializing concurrent
        // graph mutations exactly like the pg_advisory_xact_lock in the original design would.
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid parentGroupId, Guid childGroupId, CancellationToken ct)
    {
        if (parentGroupId == childGroupId)
        {
            return true;
        }

        return await _context.GroupClosures.AsNoTracking().AnyAsync(
            c => c.AncestorId == childGroupId && c.DescendantId == parentGroupId, ct);
    }

    public async Task RecomputeClosureAsync(CancellationToken ct)
    {
        var edges = await _context.GroupMembers
            .Where(m => m.MemberType == MemberType.Group)
            .Select(m => new { Ancestor = m.GroupId, Descendant = m.MemberId })
            .ToListAsync(ct);

        var groupIds = await _context.Groups.Select(g => g.Id).ToListAsync(ct);

        var adjacency = edges
            .GroupBy(e => e.Ancestor)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Descendant).ToList());

        var closureRows = new List<GroupClosure>();
        foreach (var groupId in groupIds)
        {
            closureRows.AddRange(ComputeReachableWithDepth(groupId, adjacency)
                .Select(pair => new GroupClosure { AncestorId = groupId, DescendantId = pair.Key, Depth = pair.Value }));
        }

        if (closureRows.Any(r => r.AncestorId == r.DescendantId && r.Depth != 0))
        {
            throw new InvalidOperationException(
                "group_closure integrity check failed: found a self-referencing row with depth > 0 after recomputation.");
        }

        await _context.GroupClosures.ExecuteDeleteAsync(ct);
        await _context.GroupClosures.AddRangeAsync(closureRows, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task EnsureReflexiveClosureAsync(Guid groupId, CancellationToken ct)
    {
        var exists = await _context.GroupClosures.AnyAsync(
            c => c.AncestorId == groupId && c.DescendantId == groupId, ct);

        if (!exists)
        {
            _context.GroupClosures.Add(new GroupClosure { AncestorId = groupId, DescendantId = groupId, Depth = 0 });
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>Breadth-first traversal from <paramref name="start"/> following ancestor→descendant
    /// edges, returning the shortest depth to every node it can reach (including itself, at depth 0).
    /// Mirrors the depth &lt; 32 safety cap from the reference recursive CTE in section 6.3.</summary>
    private static Dictionary<Guid, int> ComputeReachableWithDepth(Guid start, Dictionary<Guid, List<Guid>> adjacency)
    {
        var depths = new Dictionary<Guid, int> { [start] = 0 };
        var queue = new Queue<(Guid Node, int Depth)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= MaxDepth || !adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                if (!depths.ContainsKey(next))
                {
                    depths[next] = depth + 1;
                    queue.Enqueue((next, depth + 1));
                }
            }
        }

        return depths;
    }
}

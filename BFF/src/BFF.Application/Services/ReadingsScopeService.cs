using BFF.Context;
using BFF.Contracts.Readings;
using BFF.Models.Constants;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

/// <summary>Scope of the «Логи» window. A user with <c>readings</c> read sees any object; anyone else sees
/// the objects of tasks they are assigned to while the work is still open — engineers follow the
/// objects of their own tasks and lose them once the report is filed. An object always brings its
/// descendants along: a collector's log is the log of every object under it.</summary>
public sealed class ReadingsScopeService : IReadingsScopeService
{
    // Task statuses in which the assigned engineer is still on the job (Completed = report filed).
    private static readonly WorkTaskStatus[] OpenForEngineer =
    {
        WorkTaskStatus.Assigned, WorkTaskStatus.EngineerWorking, WorkTaskStatus.ReturnedToWork,
    };

    private readonly BffDbContext _context;
    private readonly IPermissionService _permissionService;

    public ReadingsScopeService(BffDbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<ReadingsScopeDto> GetScopeAsync(Guid userId, IReadOnlyCollection<int> objectIds, CancellationToken ct)
    {
        var all = await _permissionService.HasPermissionAsync(userId, ResourceCodes.Readings, PermissionFlags.Read, ct);
        if (all && objectIds.Count == 0)
        {
            return new ReadingsScopeDto { All = true };
        }

        // The object tree is small (district → collectors → objects), so it is walked in memory.
        var tree = await _context.Objects.AsNoTracking()
            .Select(o => new { o.Id, o.ParentId })
            .ToListAsync(ct);
        var known = tree.Select(o => o.Id).ToHashSet();
        var children = tree.Where(o => o.ParentId != null).ToLookup(o => o.ParentId!.Value, o => o.Id);

        HashSet<int>? allowed = null;
        IReadOnlyCollection<int> roots = objectIds;
        if (!all)
        {
            var taskObjects = await _context.TaskAssignments.AsNoTracking()
                .Where(a => a.EngineerId == userId)
                .Join(_context.Tasks.AsNoTracking(), a => a.TaskId, t => t.Id, (a, t) => t)
                .Where(t => OpenForEngineer.Contains(t.Status))
                .Select(t => t.ObjectId)
                .Distinct()
                .ToListAsync(ct);
            allowed = WithDescendants(taskObjects, children);
            if (objectIds.Count == 0)
            {
                roots = taskObjects;
            }
        }

        var visibleRoots = new List<int>();
        var visible = new HashSet<int>();
        foreach (var root in roots.Where(known.Contains).Distinct().OrderBy(id => id))
        {
            var subtree = WithDescendants(new[] { root }, children);
            if (allowed is not null)
            {
                subtree.IntersectWith(allowed);
            }

            if (subtree.Count > 0)
            {
                visibleRoots.Add(root);
                visible.UnionWith(subtree);
            }
        }

        var visibleIds = visible.ToArray();
        var sensorIds = visibleIds.Length == 0
            ? new List<int>()
            : await _context.Sensors.AsNoTracking()
                .Where(s => visibleIds.Contains(s.ObjectId))
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .ToListAsync(ct);

        return new ReadingsScopeDto { All = all, ObjectIds = visibleRoots, SensorIds = sensorIds };
    }

    private static HashSet<int> WithDescendants(IEnumerable<int> roots, ILookup<int, int> children)
    {
        var result = new HashSet<int>();
        var stack = new Stack<int>(roots);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id))
            {
                continue;
            }

            foreach (var child in children[id])
            {
                stack.Push(child);
            }
        }

        return result;
    }
}

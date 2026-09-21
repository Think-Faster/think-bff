using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Context.Repositories;
using BFF.Contracts.Common;
using BFF.Contracts.Groups;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class GroupService : IGroupService
{
    private readonly BffDbContext _context;
    private readonly IGroupGraphRepository _graphRepository;

    public GroupService(BffDbContext context, IGroupGraphRepository graphRepository)
    {
        _context = context;
        _graphRepository = graphRepository;
    }

    public async Task<PagedResult<GroupListItemDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Groups.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(g => EF.Functions.ILike(g.Name, pattern) || EF.Functions.ILike(g.Code, pattern));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GroupListItemDto { Id = g.Id, Code = g.Code, Name = g.Name, IsSystem = g.IsSystem })
            .ToListAsync(ct);

        return new PagedResult<GroupListItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<GroupDto> GetAsync(Guid id, CancellationToken ct)
    {
        var group = await _context.Groups.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Group {id} not found.");

        var members = await GetDirectMembersAsync(id, ct);
        return ToDto(group, members);
    }

    public async Task<GroupDto> CreateAsync(CreateGroupRequest request, CancellationToken ct)
    {
        var codeExists = await _context.Groups.AsNoTracking().AnyAsync(g => g.Code == request.Code, ct);
        if (codeExists)
        {
            throw new ConflictException($"Group code '{request.Code}' already exists.", "duplicate_code");
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Groups.Add(group);
        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        // Without this reflexive row, a permission query for this group's own grants finds nothing until
        // the first graph mutation recomputes the closure (section 13's note on GroupService.CreateAsync).
        await _graphRepository.EnsureReflexiveClosureAsync(group.Id, ct);

        return ToDto(group, Array.Empty<GroupMemberDto>());
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct)
    {
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Group {id} not found.");

        group.Name = request.Name;
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        var members = await GetDirectMembersAsync(id, ct);
        return ToDto(group, members);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var group = await _context.Groups.SingleOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Group {id} not found.");

        if (group.IsSystem)
        {
            throw new ConflictException($"System group '{group.Code}' cannot be deleted.", "system_group_protected");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await _graphRepository.LockGraphAsync(ct);

        await _context.GroupMembers
            .Where(m => m.GroupId == id || (m.MemberType == MemberType.Group && m.MemberId == id))
            .ExecuteDeleteAsync(ct);

        await _context.AccessGrants
            .Where(a => a.PrincipalType == PrincipalType.Group && a.PrincipalId == id)
            .ExecuteDeleteAsync(ct);

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync(ct);

        await _graphRepository.RecomputeClosureAsync(ct);

        await transaction.CommitAsync(ct);
    }

    public async Task AddMemberAsync(Guid id, AddGroupMemberRequest request, CancellationToken ct)
    {
        if (!MemberTypeExtensions.TryParse(request.MemberType, out var memberType))
        {
            throw new ArgumentException($"Unknown member type: {request.MemberType}", nameof(request));
        }

        await EnsureGroupExistsAsync(id, ct);
        await EnsureMemberExistsAsync(memberType, request.MemberId, ct);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await _graphRepository.LockGraphAsync(ct);

        if (memberType == MemberType.Group
            && await _graphRepository.WouldCreateCycleAsync(id, request.MemberId, ct))
        {
            throw new CycleDetectedException($"Adding group {request.MemberId} to {id} would create a cycle.");
        }

        var exists = await _context.GroupMembers.AnyAsync(
            m => m.GroupId == id && m.MemberType == memberType && m.MemberId == request.MemberId, ct);

        if (!exists)
        {
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = id,
                MemberType = memberType,
                MemberId = request.MemberId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await _context.SaveChangesAsync(ct);

            if (memberType == MemberType.Group)
            {
                await _graphRepository.RecomputeClosureAsync(ct);
            }
        }

        await transaction.CommitAsync(ct);
    }

    public async Task AddMembersBatchAsync(Guid id, AddGroupMembersBatchRequest request, CancellationToken ct)
    {
        await EnsureGroupExistsAsync(id, ct);

        var parsedMembers = new List<(MemberType Type, Guid MemberId)>();
        foreach (var member in request.Members)
        {
            if (!MemberTypeExtensions.TryParse(member.MemberType, out var type))
            {
                throw new ArgumentException($"Unknown member type: {member.MemberType}", nameof(request));
            }

            await EnsureMemberExistsAsync(type, member.MemberId, ct);
            parsedMembers.Add((type, member.MemberId));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await _graphRepository.LockGraphAsync(ct);

        // Cycle check across the whole batch: existing group edges plus every group edge already accepted
        // earlier in this same call, so two edges submitted together (e.g. A->B and B->A) are still caught
        // even though group_closure is only recomputed once, at the end (section 10.2's "batch, single
        // recompute" requirement).
        var existingEdges = await _context.GroupMembers.AsNoTracking()
            .Where(m => m.MemberType == MemberType.Group)
            .Select(m => new { Ancestor = m.GroupId, Descendant = m.MemberId })
            .ToListAsync(ct);

        var adjacency = existingEdges
            .GroupBy(e => e.Ancestor)
            .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(e => e.Descendant)));

        foreach (var (type, memberId) in parsedMembers.Where(m => m.Type == MemberType.Group))
        {
            if (memberId == id || IsReachable(adjacency, memberId, id))
            {
                throw new CycleDetectedException($"Adding group {memberId} to {id} would create a cycle.");
            }

            if (!adjacency.TryGetValue(id, out var descendants))
            {
                descendants = new HashSet<Guid>();
                adjacency[id] = descendants;
            }

            descendants.Add(memberId);
        }

        var addedGroupEdge = false;
        foreach (var (type, memberId) in parsedMembers)
        {
            var exists = await _context.GroupMembers.AnyAsync(
                m => m.GroupId == id && m.MemberType == type && m.MemberId == memberId, ct);

            if (!exists)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    GroupId = id,
                    MemberType = type,
                    MemberId = memberId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });

                addedGroupEdge |= type == MemberType.Group;
            }
        }

        await _context.SaveChangesAsync(ct);

        if (addedGroupEdge)
        {
            await _graphRepository.RecomputeClosureAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid id, string memberType, Guid memberId, CancellationToken ct)
    {
        if (!MemberTypeExtensions.TryParse(memberType, out var type))
        {
            throw new ArgumentException($"Unknown member type: {memberType}", nameof(memberType));
        }

        var membership = await _context.GroupMembers.SingleOrDefaultAsync(
            m => m.GroupId == id && m.MemberType == type && m.MemberId == memberId, ct);

        if (membership is null)
        {
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        _context.GroupMembers.Remove(membership);
        await _graphRepository.LockGraphAsync(ct);
        await _context.SaveChangesAsync(ct);

        if (type == MemberType.Group)
        {
            await _graphRepository.RecomputeClosureAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    private async Task EnsureGroupExistsAsync(Guid groupId, CancellationToken ct)
    {
        var exists = await _context.Groups.AsNoTracking().AnyAsync(g => g.Id == groupId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Group {groupId} not found.");
        }
    }

    private async Task EnsureMemberExistsAsync(MemberType type, Guid memberId, CancellationToken ct)
    {
        var exists = type == MemberType.User
            ? await _context.Users.AsNoTracking().AnyAsync(u => u.Id == memberId, ct)
            : await _context.Groups.AsNoTracking().AnyAsync(g => g.Id == memberId, ct);

        if (!exists)
        {
            throw new NotFoundException($"{type} {memberId} not found.");
        }
    }

    /// <summary>True if <paramref name="to"/> is reachable from <paramref name="from"/> by following
    /// ancestor→descendant edges — i.e. <paramref name="from"/> is an (in)direct ancestor of
    /// <paramref name="to"/>. Used for the in-memory cycle check in <see cref="AddMembersBatchAsync"/>.</summary>
    private static bool IsReachable(Dictionary<Guid, HashSet<Guid>> adjacency, Guid from, Guid to)
    {
        if (from == to)
        {
            return true;
        }

        var visited = new HashSet<Guid> { from };
        var queue = new Queue<Guid>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                if (next == to)
                {
                    return true;
                }

                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Direct (first-level) members of a group, both users and nested groups. Implemented as three plain
    /// EF LINQ queries (edges, then a batched lookup per member type) rather than one query with two
    /// LEFT JOINs, because group_members.member_id is a polymorphic reference with no FK EF could join
    /// through directly; each of the three queries still hits its table's primary/composite index.
    /// </summary>
    private async Task<List<GroupMemberDto>> GetDirectMembersAsync(Guid groupId, CancellationToken ct)
    {
        var edges = await _context.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .ToListAsync(ct);

        if (edges.Count == 0)
        {
            return new List<GroupMemberDto>();
        }

        var userIds = edges.Where(m => m.MemberType == MemberType.User).Select(m => m.MemberId).ToList();
        var groupIds = edges.Where(m => m.MemberType == MemberType.Group).Select(m => m.MemberId).ToList();

        var userNames = userIds.Count > 0
            ? await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, FormatFullName, ct)
            : new Dictionary<Guid, string>();

        var nestedGroups = groupIds.Count > 0
            ? await _context.Groups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, ct)
            : new Dictionary<Guid, Group>();

        var result = new List<GroupMemberDto>();
        foreach (var edge in edges)
        {
            if (edge.MemberType == MemberType.User && userNames.TryGetValue(edge.MemberId, out var displayName))
            {
                result.Add(new GroupMemberDto { Type = "user", Id = edge.MemberId, DisplayName = displayName });
            }
            else if (edge.MemberType == MemberType.Group && nestedGroups.TryGetValue(edge.MemberId, out var nested))
            {
                result.Add(new GroupMemberDto { Type = "group", Id = nested.Id, DisplayName = nested.Name, Code = nested.Code });
            }
        }

        return result;
    }

    private static string FormatFullName(User u) =>
        string.Join(' ', new[] { u.LastName, u.FirstName, u.MiddleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static GroupDto ToDto(Group g, IReadOnlyList<GroupMemberDto> members) => new()
    {
        Id = g.Id,
        Code = g.Code,
        Name = g.Name,
        IsSystem = g.IsSystem,
        Members = members,
    };
}

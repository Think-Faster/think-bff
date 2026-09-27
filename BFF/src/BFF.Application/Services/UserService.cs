using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Groups;
using BFF.Contracts.Users;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class UserService : IUserService
{
    private readonly BffDbContext _context;

    public UserService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UserListItemDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.LastName, pattern) ||
                EF.Functions.ILike(u.FirstName, pattern) ||
                (u.MiddleName != null && EF.Functions.ILike(u.MiddleName, pattern)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => ToListItemDto(u))
            .ToListAsync(ct);

        return new PagedResult<UserListItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"User {id} not found.");

        var groups = await GetDirectGroupsAsync(id, ct);

        return ToDto(user, groups);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            AuthUserId = request.AuthUserId,
            LastName = request.LastName,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            Email = request.Email,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Users.Add(user);

        if (request.GroupIds is { Count: > 0 })
        {
            await AddGroupMembershipsAsync(user.Id, request.GroupIds, ct);
        }

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        var groups = await GetDirectGroupsAsync(user.Id, ct);
        return ToDto(user, groups);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"User {id} not found.");

        user.LastName = request.LastName;
        user.FirstName = request.FirstName;
        user.MiddleName = request.MiddleName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        var groups = await GetDirectGroupsAsync(id, ct);
        return ToDto(user, groups);
    }

    public async Task DeleteAsync(Guid id, bool soft, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"User {id} not found.");

        if (soft)
        {
            user.IsActive = false;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            await _context.GroupMembers
                .Where(m => m.MemberType == MemberType.User && m.MemberId == id)
                .ExecuteDeleteAsync(ct);

            await _context.AccessGrants
                .Where(g => g.PrincipalType == PrincipalType.User && g.PrincipalId == id)
                .ExecuteDeleteAsync(ct);

            _context.Users.Remove(user);
        }

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GroupRefDto>> AddGroupsAsync(Guid id, AddUserGroupsRequest request, CancellationToken ct)
    {
        var userExists = await _context.Users.AsNoTracking().AnyAsync(u => u.Id == id, ct);
        if (!userExists)
        {
            throw new NotFoundException($"User {id} not found.");
        }

        await AddGroupMembershipsAsync(id, request.GroupIds, ct);

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        return await GetDirectGroupsAsync(id, ct);
    }

    public async Task RemoveGroupAsync(Guid id, Guid groupId, CancellationToken ct)
    {
        var membership = await _context.GroupMembers.SingleOrDefaultAsync(
            m => m.GroupId == groupId && m.MemberType == MemberType.User && m.MemberId == id, ct);

        if (membership is null)
        {
            return;
        }

        _context.GroupMembers.Remove(membership);

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<AuthenticatedUserDto?> FindByAuthUserIdAsync(string authUserId, CancellationToken ct)
    {
        var user = await _context.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.AuthUserId == authUserId, ct);

        return user is null
            ? null
            : new AuthenticatedUserDto
            {
                Id = user.Id,
                AuthUserId = user.AuthUserId,
                FullName = FormatFullName(user),
                IsActive = user.IsActive,
            };
    }

    private async Task AddGroupMembershipsAsync(Guid userId, IReadOnlyList<Guid> groupIds, CancellationToken ct)
    {
        var distinctIds = groupIds.Distinct().ToList();

        var existingGroupIds = await _context.Groups.AsNoTracking()
            .Where(g => distinctIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        var missing = distinctIds.Except(existingGroupIds).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"Group(s) not found: {string.Join(", ", missing)}.");
        }

        var alreadyMember = await _context.GroupMembers.AsNoTracking()
            .Where(m => m.MemberType == MemberType.User && m.MemberId == userId && distinctIds.Contains(m.GroupId))
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        var toAdd = distinctIds.Except(alreadyMember);

        foreach (var groupId in toAdd)
        {
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                MemberType = MemberType.User,
                MemberId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private async Task<List<GroupRefDto>> GetDirectGroupsAsync(Guid userId, CancellationToken ct)
    {
        return await (
                from m in _context.GroupMembers.AsNoTracking()
                join g in _context.Groups.AsNoTracking() on m.GroupId equals g.Id
                where m.MemberType == MemberType.User && m.MemberId == userId
                orderby g.Name
                select new GroupRefDto { Id = g.Id, Code = g.Code, Name = g.Name })
            .ToListAsync(ct);
    }

    private static string FormatFullName(User u) =>
        string.Join(' ', new[] { u.LastName, u.FirstName, u.MiddleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public async Task<IReadOnlyList<UserEmailDto>> ResolveEmailsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var distinctIds = userIds.Distinct().ToList();

        var found = await _context.Users.AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new UserEmailDto { UserId = u.Id, Found = true, Email = u.Email })
            .ToListAsync(ct);

        var foundIds = found.Select(f => f.UserId).ToHashSet();
        var missing = distinctIds.Except(foundIds)
            .Select(id => new UserEmailDto { UserId = id, Found = false, Email = null });

        return found.Concat(missing).ToList();
    }

    private static UserListItemDto ToListItemDto(User u) => new()
    {
        Id = u.Id,
        AuthUserId = u.AuthUserId,
        LastName = u.LastName,
        FirstName = u.FirstName,
        MiddleName = u.MiddleName,
        Email = u.Email,
        IsActive = u.IsActive,
    };

    private static UserDto ToDto(User u, IReadOnlyList<GroupRefDto> groups) => new()
    {
        Id = u.Id,
        AuthUserId = u.AuthUserId,
        LastName = u.LastName,
        FirstName = u.FirstName,
        MiddleName = u.MiddleName,
        Email = u.Email,
        IsActive = u.IsActive,
        Groups = groups,
    };
}

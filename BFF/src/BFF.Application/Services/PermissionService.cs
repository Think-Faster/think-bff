using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Permissions;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly BffDbContext _context;
    private readonly IPermissionCache _cache;

    public PermissionService(BffDbContext context, IPermissionCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<MyPermissionsResponse> GetMyPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var permissions = await _cache.GetPermissionsAsync(userId, ct);

        var dict = permissions.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.ToNames());

        return new MyPermissionsResponse { UserId = userId, Permissions = dict };
    }

    public async Task<bool> CheckAsync(Guid userId, string resourceCode, string permission, CancellationToken ct)
    {
        if (!PermissionFlagsExtensions.TryParseName(permission, out var flag))
        {
            throw new ArgumentException($"Unknown permission: {permission}", nameof(permission));
        }

        return await HasPermissionAsync(userId, resourceCode, flag, ct);
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string resourceCode, PermissionFlags required, CancellationToken ct)
    {
        var permissions = await _cache.GetPermissionsAsync(userId, ct);
        if (!permissions.TryGetValue(resourceCode, out var mask))
        {
            return false;
        }

        return (mask & required) == required || (mask & PermissionFlags.Manage) != 0;
    }

    public async Task<IReadOnlyList<GrantDto>> GetGrantsAsync(string? principalType, Guid? principalId, CancellationToken ct)
    {
        var query = _context.AccessGrants.AsNoTracking().Include(g => g.Resource).AsQueryable();

        if (!string.IsNullOrWhiteSpace(principalType))
        {
            if (!PrincipalTypeExtensions.TryParse(principalType, out var type))
            {
                throw new ArgumentException($"Unknown principal type: {principalType}", nameof(principalType));
            }

            query = query.Where(g => g.PrincipalType == type);
        }

        if (principalId is { } id)
        {
            query = query.Where(g => g.PrincipalId == id);
        }

        var grants = await query.OrderBy(g => g.CreatedAt).ToListAsync(ct);
        return grants.Select(ToDto).ToArray();
    }

    public async Task<GrantDto> UpsertGrantAsync(CreateGrantRequest request, CancellationToken ct)
    {
        if (!PrincipalTypeExtensions.TryParse(request.PrincipalType, out var principalType))
        {
            throw new ArgumentException($"Unknown principal type: {request.PrincipalType}", nameof(request));
        }

        var mask = PermissionFlagsExtensions.ParseNames(request.Permissions);

        var resource = await _context.Resources.SingleOrDefaultAsync(r => r.Code == request.ResourceCode, ct)
            ?? throw new NotFoundException($"Resource '{request.ResourceCode}' not found.");

        var grant = await _context.AccessGrants.SingleOrDefaultAsync(
            g => g.PrincipalType == principalType && g.PrincipalId == request.PrincipalId && g.ResourceId == resource.Id, ct);

        if (grant is null)
        {
            grant = new AccessGrant
            {
                Id = Guid.NewGuid(),
                PrincipalType = principalType,
                PrincipalId = request.PrincipalId,
                ResourceId = resource.Id,
                PermissionMask = mask,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _context.AccessGrants.Add(grant);
        }
        else
        {
            grant.PermissionMask = mask;
            grant.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);

        grant.Resource = resource;
        return ToDto(grant);
    }

    public async Task RevokeGrantAsync(Guid id, CancellationToken ct)
    {
        var grant = await _context.AccessGrants.SingleOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Grant {id} not found.");

        _context.AccessGrants.Remove(grant);

        await _context.MarkRbacVersionForIncrementAsync(ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ResourceDto>> GetResourcesAsync(CancellationToken ct)
    {
        return await _context.Resources.AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new ResourceDto { Id = r.Id, Code = r.Code, Name = r.Name })
            .ToListAsync(ct);
    }

    public async Task<ResourceDto> CreateResourceAsync(CreateResourceRequest request, CancellationToken ct)
    {
        var exists = await _context.Resources.AsNoTracking().AnyAsync(r => r.Code == request.Code, ct);
        if (exists)
        {
            throw new ConflictException($"Resource code '{request.Code}' already exists.", "duplicate_code");
        }

        var resource = new Resource { Code = request.Code, Name = request.Name };
        _context.Resources.Add(resource);
        await _context.SaveChangesAsync(ct);

        return new ResourceDto { Id = resource.Id, Code = resource.Code, Name = resource.Name };
    }

    private static GrantDto ToDto(AccessGrant grant) => new()
    {
        Id = grant.Id,
        PrincipalType = grant.PrincipalType.ToApiString(),
        PrincipalId = grant.PrincipalId,
        ResourceCode = grant.Resource?.Code ?? string.Empty,
        Permissions = grant.PermissionMask.ToNames(),
    };
}

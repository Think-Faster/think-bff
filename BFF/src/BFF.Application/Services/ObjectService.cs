using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Objects;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class ObjectService : IObjectService
{
    private readonly BffDbContext _context;

    public ObjectService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ObjectDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Objects.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.Name, pattern) || EF.Functions.ILike(o.Kind, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(o => o.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => ToDto(o)).ToListAsync(ct);

        return new PagedResult<ObjectDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<ObjectDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Objects.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Object {id} not found.");
        return ToDto(entity);
    }

    public async Task<ObjectDto> CreateAsync(CreateObjectRequest request, CancellationToken ct)
    {
        var exists = await _context.Objects.AsNoTracking().AnyAsync(o => o.Id == request.Id, ct);
        if (exists)
        {
            throw new ConflictException($"Object {request.Id} already exists.", "duplicate_code");
        }

        var entity = new MonitoringObject
        {
            Id = request.Id,
            Level = request.Level,
            ParentId = request.ParentId,
            Kind = request.Kind,
            Name = request.Name,
            Address = request.Address,
            GeometryGeoJson = request.GeometryGeoJson,
            Status = ObjectStatus.Normal,
            StatusAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Objects.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ObjectDto> UpdateAsync(int id, UpdateObjectRequest request, CancellationToken ct)
    {
        var entity = await _context.Objects.SingleOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Object {id} not found.");

        entity.Name = request.Name;
        entity.Address = request.Address;
        entity.GeometryGeoJson = request.GeometryGeoJson;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Objects.SingleOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Object {id} not found.");

        _context.Objects.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PicketDto>> ListPicketsAsync(int objectId, CancellationToken ct)
    {
        return await _context.Pickets.AsNoTracking()
            .Where(p => p.ObjectId == objectId)
            .OrderBy(p => p.Ordinal)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
    }

    public async Task<PicketDto> CreatePicketAsync(int objectId, CreatePicketRequest request, CancellationToken ct)
    {
        await EnsureObjectExistsAsync(objectId, ct);

        var duplicateCode = await _context.Pickets.AsNoTracking()
            .AnyAsync(p => p.ObjectId == objectId && p.Code == request.Code, ct);
        if (duplicateCode)
        {
            throw new ConflictException($"Picket code '{request.Code}' already exists on object {objectId}.", "duplicate_code");
        }

        var entity = new Picket
        {
            ObjectId = objectId,
            Code = request.Code,
            Ordinal = request.Ordinal,
            GeometryGeoJson = request.GeometryGeoJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Pickets.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PicketDto> UpdatePicketAsync(int objectId, long picketId, UpdatePicketRequest request, CancellationToken ct)
    {
        var entity = await _context.Pickets.SingleOrDefaultAsync(p => p.Id == picketId && p.ObjectId == objectId, ct)
            ?? throw new NotFoundException($"Picket {picketId} not found on object {objectId}.");

        entity.Ordinal = request.Ordinal;
        entity.GeometryGeoJson = request.GeometryGeoJson;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeletePicketAsync(int objectId, long picketId, CancellationToken ct)
    {
        var entity = await _context.Pickets.SingleOrDefaultAsync(p => p.Id == picketId && p.ObjectId == objectId, ct)
            ?? throw new NotFoundException($"Picket {picketId} not found on object {objectId}.");

        _context.Pickets.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MapLayerDto>> ListLayersAsync(int objectId, short? level, CancellationToken ct)
    {
        var query = _context.MapLayers.AsNoTracking().Where(l => l.ObjectId == objectId);
        if (level is { } lvl)
        {
            query = query.Where(l => l.Level == lvl);
        }

        return await query.OrderBy(l => l.Level).Select(l => ToDto(l)).ToListAsync(ct);
    }

    public async Task<MapLayerDto> UpsertLayerAsync(int objectId, UpsertMapLayerRequest request, CancellationToken ct)
    {
        await EnsureObjectExistsAsync(objectId, ct);

        var entity = await _context.MapLayers.SingleOrDefaultAsync(
            l => l.ObjectId == objectId && l.Level == request.Level && l.Kind == request.Kind, ct);

        if (entity is null)
        {
            entity = new MapLayer
            {
                Id = Guid.NewGuid(),
                ObjectId = objectId,
                Level = request.Level,
                Kind = request.Kind,
                GeoJson = request.GeoJson,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _context.MapLayers.Add(entity);
        }
        else
        {
            entity.GeoJson = request.GeoJson;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private async Task EnsureObjectExistsAsync(int objectId, CancellationToken ct)
    {
        var exists = await _context.Objects.AsNoTracking().AnyAsync(o => o.Id == objectId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Object {objectId} not found.");
        }
    }

    private static ObjectDto ToDto(MonitoringObject o) => new()
    {
        Id = o.Id,
        Level = o.Level,
        ParentId = o.ParentId,
        Kind = o.Kind,
        Name = o.Name,
        Address = o.Address,
        GeometryGeoJson = o.GeometryGeoJson,
        Status = o.Status,
        StatusAt = o.StatusAt,
    };

    private static PicketDto ToDto(Picket p) => new()
    {
        Id = p.Id,
        ObjectId = p.ObjectId,
        Code = p.Code,
        Ordinal = p.Ordinal,
        GeometryGeoJson = p.GeometryGeoJson,
    };

    private static MapLayerDto ToDto(MapLayer l) => new()
    {
        Id = l.Id,
        Level = l.Level,
        ObjectId = l.ObjectId,
        Kind = l.Kind,
        GeoJson = l.GeoJson,
        UpdatedAt = l.UpdatedAt,
    };
}

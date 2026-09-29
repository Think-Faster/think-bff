using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Sensors;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class SensorService : ISensorService
{
    private readonly BffDbContext _context;

    public SensorService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SensorDto>> ListAsync(int? objectId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Sensors.AsNoTracking().AsQueryable();

        if (objectId is { } oid)
        {
            query = query.Where(s => s.ObjectId == oid);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => ToDto(s)).ToListAsync(ct);

        return new PagedResult<SensorDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    /// <summary>Различные пары (system, s_type) всех датчиков — справочника подсистем и типов в БД нет,
    /// поэтому варианты для формы берутся из данных.</summary>
    public async Task<IReadOnlyList<SensorTypeOptionDto>> ListTypesAsync(CancellationToken ct)
        => await _context.Sensors.AsNoTracking()
            .Select(s => new { s.System, s.SType })
            .Distinct()
            .OrderBy(s => s.System).ThenBy(s => s.SType)
            .Select(s => new SensorTypeOptionDto { System = s.System, SType = s.SType })
            .ToListAsync(ct);

    public async Task<SensorDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Sensors.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Sensor {id} not found.");
        return ToDto(entity);
    }

    public async Task<SensorDto> CreateAsync(CreateSensorRequest request, CancellationToken ct)
    {
        var exists = await _context.Sensors.AsNoTracking().AnyAsync(s => s.Id == request.Id, ct);
        if (exists)
        {
            throw new ConflictException($"Sensor {request.Id} already exists.", "duplicate_code");
        }

        var entity = new Sensor
        {
            Id = request.Id,
            ObjectId = request.ObjectId,
            PicketId = request.PicketId,
            System = request.System,
            SType = request.SType,
            Tag = request.Tag,
            Name = request.Name,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Sensors.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<SensorDto> UpdateAsync(int id, UpdateSensorRequest request, CancellationToken ct)
    {
        var entity = await _context.Sensors.SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Sensor {id} not found.");

        entity.PicketId = request.PicketId;
        entity.Name = request.Name;
        entity.Tag = request.Tag;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Sensors.SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Sensor {id} not found.");

        _context.Sensors.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SensorLinkDto>> ListLinksAsync(int sensorId, CancellationToken ct)
    {
        return await _context.SensorLinks.AsNoTracking()
            .Where(l => l.FromSensorId == sensorId)
            .Select(l => ToDto(l))
            .ToListAsync(ct);
    }

    public async Task<SensorLinkDto> CreateLinkAsync(int sensorId, CreateSensorLinkRequest request, CancellationToken ct)
    {
        var fromExists = await _context.Sensors.AsNoTracking().AnyAsync(s => s.Id == sensorId, ct);
        var toExists = await _context.Sensors.AsNoTracking().AnyAsync(s => s.Id == request.ToSensorId, ct);
        if (!fromExists || !toExists)
        {
            throw new NotFoundException("One or both sensors in the link were not found.");
        }

        var entity = new SensorLink { FromSensorId = sensorId, ToSensorId = request.ToSensorId, Kind = request.Kind };
        _context.SensorLinks.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteLinkAsync(int sensorId, int toSensorId, string kind, CancellationToken ct)
    {
        var entity = await _context.SensorLinks.SingleOrDefaultAsync(
            l => l.FromSensorId == sensorId && l.ToSensorId == toSensorId && l.Kind == kind, ct);

        if (entity is null)
        {
            return;
        }

        _context.SensorLinks.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static SensorDto ToDto(Sensor s) => new()
    {
        Id = s.Id,
        ObjectId = s.ObjectId,
        PicketId = s.PicketId,
        System = s.System,
        SType = s.SType,
        Tag = s.Tag,
        Name = s.Name,
        IsActive = s.IsActive,
    };

    private static SensorLinkDto ToDto(SensorLink l) => new()
    {
        FromSensorId = l.FromSensorId,
        ToSensorId = l.ToSensorId,
        Kind = l.Kind,
    };
}

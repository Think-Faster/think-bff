using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Common;
using BFF.Contracts.Incidents;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class IncidentService : IIncidentService
{
    private readonly BffDbContext _context;

    public IncidentService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<IncidentDto>> ListAsync(int? objectId, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Incidents.AsNoTracking().AsQueryable();
        if (objectId is { } oid)
        {
            query = query.Where(i => i.ObjectId == oid);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.StartedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => ToDto(i)).ToListAsync(ct);

        return new PagedResult<IncidentDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<IncidentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await _context.Incidents.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Incident {id} not found.");
        return ToDto(entity);
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct)
    {
        var entity = new Incident
        {
            Id = Guid.NewGuid(),
            ObjectId = request.ObjectId,
            Type = request.Type,
            StartedAt = request.StartedAt,
            TaskId = request.TaskId,
            PredictionId = request.PredictionId,
        };

        _context.Incidents.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IncidentDto> ConfirmAsync(Guid id, Guid confirmedBy, ConfirmIncidentRequest request, CancellationToken ct)
    {
        var entity = await _context.Incidents.SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Incident {id} not found.");

        entity.ConfirmedAt = DateTimeOffset.UtcNow;
        entity.ConfirmedBy = confirmedBy;
        entity.Outcome = request.Outcome;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static IncidentDto ToDto(Incident i) => new()
    {
        Id = i.Id,
        ObjectId = i.ObjectId,
        Type = i.Type,
        StartedAt = i.StartedAt,
        ConfirmedAt = i.ConfirmedAt,
        ConfirmedBy = i.ConfirmedBy,
        TaskId = i.TaskId,
        PredictionId = i.PredictionId,
        Outcome = i.Outcome,
    };
}

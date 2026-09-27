using BFF.Context;
using BFF.Contracts.Schedule;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly BffDbContext _context;

    public ScheduleService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ScheduleEntryDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _context.ScheduleEntries.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.DateFrom)
            .Select(s => ToDto(s))
            .ToListAsync(ct);
    }

    public async Task<ScheduleEntryDto> CreateAsync(Guid userId, Guid changedBy, CreateScheduleEntryRequest request, CancellationToken ct)
    {
        var entity = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Status = request.Status,
            ShiftStart = request.ShiftStart,
            ShiftHours = request.ShiftHours,
            Source = request.Source,
            ChangedBy = changedBy,
            ChangedAt = DateTimeOffset.UtcNow,
        };

        _context.ScheduleEntries.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid userId, Guid entryId, CancellationToken ct)
    {
        var entity = await _context.ScheduleEntries.SingleOrDefaultAsync(
            s => s.UserId == userId && s.Id == entryId, ct);

        if (entity is null)
        {
            return;
        }

        _context.ScheduleEntries.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static ScheduleEntryDto ToDto(ScheduleEntry s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        DateFrom = s.DateFrom,
        DateTo = s.DateTo,
        Status = s.Status,
        ShiftStart = s.ShiftStart,
        ShiftHours = s.ShiftHours,
        Source = s.Source,
        ChangedBy = s.ChangedBy,
        ChangedAt = s.ChangedAt,
    };
}

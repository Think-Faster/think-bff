using BFF.Application.Exceptions;
using BFF.Context;
using BFF.Contracts.Engineers;
using BFF.Models.Constants;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class EngineerService : IEngineerService
{
    private readonly BffDbContext _context;

    public EngineerService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BrigadeDto>> ListBrigadesAsync(CancellationToken ct)
    {
        return await _context.Brigades.AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BrigadeDto { Id = b.Id, Name = b.Name, Unit = b.Unit, LeaderId = b.LeaderId })
            .ToListAsync(ct);
    }

    public async Task<BrigadeDto> CreateBrigadeAsync(CreateBrigadeRequest request, CancellationToken ct)
    {
        var entity = new Brigade { Id = Guid.NewGuid(), Name = request.Name, Unit = request.Unit, LeaderId = request.LeaderId };
        _context.Brigades.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new BrigadeDto { Id = entity.Id, Name = entity.Name, Unit = entity.Unit, LeaderId = entity.LeaderId };
    }

    public async Task<EngineerProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var entity = await _context.EngineerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (entity is null)
        {
            return null;
        }

        var telegram = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Telegram).SingleOrDefaultAsync(ct);
        return ToDto(entity, telegram);
    }

    public async Task<EngineerProfileDto> UpsertProfileAsync(Guid userId, UpsertEngineerProfileRequest request, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException($"User {userId} not found.");
        var entity = await _context.EngineerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);

        if (entity is null)
        {
            entity = new EngineerProfile { UserId = userId };
            _context.EngineerProfiles.Add(entity);
        }

        entity.BrigadeId = request.BrigadeId;
        entity.Phone = request.Phone;
        if (request.Telegram is not null)
        {
            user.Telegram = TelegramUsername.Normalize(request.Telegram);
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        entity.Specialization = request.Specialization?.ToArray() ?? entity.Specialization;
        entity.Status = request.Status;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity, user.Telegram);
    }

    public async Task<IReadOnlyList<EngineerPermitDto>> ListPermitsAsync(Guid userId, CancellationToken ct)
    {
        return await _context.EngineerPermits.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Kind).ThenByDescending(p => p.ValidUntil)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
    }

    public async Task<EngineerPermitDto> CreatePermitAsync(Guid userId, Guid checkedBy, CreateEngineerPermitRequest request, CancellationToken ct)
    {
        var entity = new EngineerPermit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = request.Kind,
            Level = request.Level,
            ValidUntil = request.ValidUntil,
            DocumentNo = request.DocumentNo,
            CheckedBy = checkedBy,
            CheckedAt = request.CheckedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };

        _context.EngineerPermits.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeletePermitAsync(Guid userId, Guid permitId, CancellationToken ct)
    {
        var entity = await _context.EngineerPermits.SingleOrDefaultAsync(
            p => p.UserId == userId && p.Id == permitId, ct);

        if (entity is null)
        {
            return;
        }

        _context.EngineerPermits.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static EngineerPermitDto ToDto(EngineerPermit p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        Kind = p.Kind,
        Level = p.Level,
        ValidUntil = p.ValidUntil,
        DocumentNo = p.DocumentNo,
        CheckedBy = p.CheckedBy,
        CheckedAt = p.CheckedAt,
    };

    private static EngineerProfileDto ToDto(EngineerProfile p, string? telegram) => new()
    {
        UserId = p.UserId,
        BrigadeId = p.BrigadeId,
        Phone = p.Phone,
        Telegram = telegram,
        Specialization = p.Specialization,
        Status = p.Status,
    };
}

using BFF.Context;
using BFF.Contracts.Engineers;
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
            .Select(b => new BrigadeDto { Id = b.Id, Name = b.Name })
            .ToListAsync(ct);
    }

    public async Task<BrigadeDto> CreateBrigadeAsync(CreateBrigadeRequest request, CancellationToken ct)
    {
        var entity = new Brigade { Id = Guid.NewGuid(), Name = request.Name };
        _context.Brigades.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new BrigadeDto { Id = entity.Id, Name = entity.Name };
    }

    public async Task<EngineerProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var entity = await _context.EngineerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<EngineerProfileDto> UpsertProfileAsync(Guid userId, UpsertEngineerProfileRequest request, CancellationToken ct)
    {
        var entity = await _context.EngineerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);

        if (entity is null)
        {
            entity = new EngineerProfile { UserId = userId };
            _context.EngineerProfiles.Add(entity);
        }

        entity.BrigadeId = request.BrigadeId;
        entity.Phone = request.Phone;
        entity.Telegram = request.Telegram;
        entity.Specialization = request.Specialization?.ToArray() ?? entity.Specialization;
        entity.Status = request.Status;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static EngineerProfileDto ToDto(EngineerProfile p) => new()
    {
        UserId = p.UserId,
        BrigadeId = p.BrigadeId,
        Phone = p.Phone,
        Telegram = p.Telegram,
        Specialization = p.Specialization,
        Status = p.Status,
    };
}

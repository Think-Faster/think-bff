using BFF.Context;
using BFF.Contracts.Users;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

public sealed class AssignedObjectService : IAssignedObjectService
{
    private readonly BffDbContext _context;

    public AssignedObjectService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AssignedObjectDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _context.AssignedObjects.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    public async Task<AssignedObjectDto> AssignAsync(Guid userId, Guid assignedBy, AssignObjectRequest request, CancellationToken ct)
    {
        var existing = await _context.AssignedObjects.SingleOrDefaultAsync(
            a => a.UserId == userId && a.ObjectId == request.ObjectId, ct);

        if (existing is not null)
        {
            existing.Note = request.Note;
            await _context.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var entity = new AssignedObject
        {
            UserId = userId,
            ObjectId = request.ObjectId,
            AssignedBy = assignedBy,
            AssignedAt = DateTimeOffset.UtcNow,
            Note = request.Note,
        };

        _context.AssignedObjects.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UnassignAsync(Guid userId, int objectId, CancellationToken ct)
    {
        var entity = await _context.AssignedObjects.SingleOrDefaultAsync(
            a => a.UserId == userId && a.ObjectId == objectId, ct);

        if (entity is null)
        {
            return;
        }

        _context.AssignedObjects.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static AssignedObjectDto ToDto(AssignedObject a) => new()
    {
        UserId = a.UserId,
        ObjectId = a.ObjectId,
        AssignedBy = a.AssignedBy,
        AssignedAt = a.AssignedAt,
        Note = a.Note,
    };
}

using BFF.Contracts.Users;

namespace BFF.Application.Services;

public interface IAssignedObjectService
{
    Task<IReadOnlyList<AssignedObjectDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<AssignedObjectDto> AssignAsync(Guid userId, Guid assignedBy, AssignObjectRequest request, CancellationToken ct);
    Task UnassignAsync(Guid userId, int objectId, CancellationToken ct);
}

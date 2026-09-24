using BFF.Contracts.Common;
using BFF.Contracts.Incidents;

namespace BFF.Application.Services;

public interface IIncidentService
{
    Task<PagedResult<IncidentDto>> ListAsync(int? objectId, int page, int pageSize, CancellationToken ct);
    Task<IncidentDto> GetAsync(Guid id, CancellationToken ct);
    Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct);
    Task<IncidentDto> ConfirmAsync(Guid id, Guid confirmedBy, ConfirmIncidentRequest request, CancellationToken ct);
}

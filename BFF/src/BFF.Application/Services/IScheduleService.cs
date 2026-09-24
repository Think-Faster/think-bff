using BFF.Contracts.Schedule;

namespace BFF.Application.Services;

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduleEntryDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<ScheduleEntryDto> CreateAsync(Guid userId, Guid changedBy, CreateScheduleEntryRequest request, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid entryId, CancellationToken ct);
}

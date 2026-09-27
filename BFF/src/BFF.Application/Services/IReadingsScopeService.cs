using BFF.Contracts.Readings;

namespace BFF.Application.Services;

public interface IReadingsScopeService
{
    Task<ReadingsScopeDto> GetScopeAsync(Guid userId, IReadOnlyCollection<int> objectIds, CancellationToken ct);
}

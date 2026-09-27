using BFF.Contracts.Common;
using BFF.Contracts.Sensors;

namespace BFF.Application.Services;

public interface ISensorService
{
    Task<PagedResult<SensorDto>> ListAsync(int? objectId, string? search, int page, int pageSize, CancellationToken ct);
    Task<SensorDto> GetAsync(int id, CancellationToken ct);
    Task<SensorDto> CreateAsync(CreateSensorRequest request, CancellationToken ct);
    Task<SensorDto> UpdateAsync(int id, UpdateSensorRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<SensorLinkDto>> ListLinksAsync(int sensorId, CancellationToken ct);
    Task<SensorLinkDto> CreateLinkAsync(int sensorId, CreateSensorLinkRequest request, CancellationToken ct);
    Task DeleteLinkAsync(int sensorId, int toSensorId, string kind, CancellationToken ct);
}

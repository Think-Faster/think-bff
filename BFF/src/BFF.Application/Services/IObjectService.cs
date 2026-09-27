using BFF.Contracts.Common;
using BFF.Contracts.Objects;

namespace BFF.Application.Services;

public interface IObjectService
{
    Task<PagedResult<ObjectDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<ObjectDto> GetAsync(int id, CancellationToken ct);
    Task<ObjectDto> CreateAsync(CreateObjectRequest request, CancellationToken ct);
    Task<ObjectDto> UpdateAsync(int id, UpdateObjectRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<PicketDto>> ListPicketsAsync(int objectId, CancellationToken ct);
    Task<PicketDto> CreatePicketAsync(int objectId, CreatePicketRequest request, CancellationToken ct);
    Task<PicketDto> UpdatePicketAsync(int objectId, long picketId, UpdatePicketRequest request, CancellationToken ct);
    Task DeletePicketAsync(int objectId, long picketId, CancellationToken ct);

    Task<IReadOnlyList<MapLayerDto>> ListLayersAsync(int objectId, short? level, CancellationToken ct);
    Task<MapLayerDto> UpsertLayerAsync(int objectId, UpsertMapLayerRequest request, CancellationToken ct);
}

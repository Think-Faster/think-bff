using BFF.Contracts.Engineers;

namespace BFF.Application.Services;

public interface IEngineerService
{
    Task<IReadOnlyList<BrigadeDto>> ListBrigadesAsync(CancellationToken ct);
    Task<BrigadeDto> CreateBrigadeAsync(CreateBrigadeRequest request, CancellationToken ct);

    Task<EngineerProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<EngineerProfileDto> UpsertProfileAsync(Guid userId, UpsertEngineerProfileRequest request, CancellationToken ct);
}

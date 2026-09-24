using BFF.Contracts.ModelSettings;

namespace BFF.Application.Services;

public interface IModelSettingsService
{
    Task<IReadOnlyList<ModelVersionDto>> ListModelVersionsAsync(CancellationToken ct);
    Task<ModelVersionDto> CreateModelVersionAsync(CreateModelVersionRequest request, CancellationToken ct);
    Task<ModelVersionDto> ActivateModelVersionAsync(string id, Guid switchedBy, CancellationToken ct);

    Task<IReadOnlyList<CoefficientDto>> ListCoefficientsAsync(CancellationToken ct);
    Task<CoefficientDto> CreateCoefficientAsync(Guid createdBy, CreateCoefficientRequest request, CancellationToken ct);

    Task<IReadOnlyList<RetrainJobDto>> ListRetrainJobsAsync(CancellationToken ct);
    Task<RetrainJobDto> CreateRetrainJobAsync(Guid requestedBy, CreateRetrainJobRequest request, CancellationToken ct);

    Task<IReadOnlyList<IgnoredRangeDto>> ListIgnoredRangesAsync(CancellationToken ct);
    Task<IgnoredRangeDto> CreateIgnoredRangeAsync(Guid createdBy, CreateIgnoredRangeRequest request, CancellationToken ct);
    Task DeleteIgnoredRangeAsync(Guid id, CancellationToken ct);
}

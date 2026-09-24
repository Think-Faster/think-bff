using BFF.Contracts.Common;
using BFF.Contracts.Predictions;

namespace BFF.Application.Services;

public interface IPredictionService
{
    Task<PagedResult<PredictionListItemDto>> ListAsync(
        int? objectId, string? status, int page, int pageSize, CancellationToken ct);
    Task<PredictionDto> GetAsync(Guid id, CancellationToken ct);
    Task<PredictionDto> CreateAsync(CreatePredictionRequest request, CancellationToken ct);

    Task<PredictionDecisionDto> DecideAsync(Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, CancellationToken ct);

    Task<PagedResult<FactAlertDto>> ListFactAlertsAsync(int? objectId, int page, int pageSize, CancellationToken ct);
    Task<FactAlertDto> CreateFactAlertAsync(CreateFactAlertRequest request, CancellationToken ct);
}

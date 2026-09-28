using BFF.Contracts.Common;
using BFF.Contracts.Predictions;

namespace BFF.Application.Services;

public interface IPredictionService
{
    Task<PagedResult<PredictionListItemDto>> ListAsync(
        int? objectId, string? status, int page, int pageSize, CancellationToken ct);
    Task<PredictionDto> GetAsync(Guid id, CancellationToken ct);
    Task<PredictionDto> CreateAsync(CreatePredictionRequest request, CancellationToken ct);

    /// <summary>Тревога модели из tf.forecast.results (kind "forecast"). Модель шлёт её каждый час, пока
    /// тревога горит: тот же эпизод пары объект-тип (последний прогноз не старше начала эпизода по
    /// since_hours) обновляет карточку, статус и решение диспетчера не трогая; новый эпизод — новая
    /// карточка (Created=true).</summary>
    Task<(PredictionDto Prediction, bool Created)> RecordForecastAsync(CreatePredictionRequest request, CancellationToken ct);

    Task<PredictionDecisionDto> DecideAsync(Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, CancellationToken ct);

    Task<PagedResult<FactAlertDto>> ListFactAlertsAsync(int? objectId, int page, int pageSize, CancellationToken ct);
    Task<FactAlertDto> CreateFactAlertAsync(CreateFactAlertRequest request, CancellationToken ct);

    /// <summary>Объявление по факту из tf.forecast.results: то же, что CreateFactAlertAsync, но повтор
    /// (тот же объект, тип и начало эпизода) не создаёт второй записи — Created=false.</summary>
    Task<(FactAlertDto Alert, bool Created)> RecordFactAlertAsync(CreateFactAlertRequest request, CancellationToken ct);
}

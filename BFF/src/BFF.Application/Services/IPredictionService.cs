using BFF.Contracts.Common;
using BFF.Contracts.Predictions;
using BFF.Models.Enums;

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

    /// <summary>Тревога модели кончилась (alarm=false в часе hourEnd, §9.8): у карточек пары с горящей тревогой
    /// Alarm=false и AlarmEndedAt; без решения (New, InReview) — Expired. Возвращает, сколько карточек задело.</summary>
    Task<int> EndAlarmsAsync(int objectId, IReadOnlyCollection<PredictionType> types, DateTimeOffset hourEnd, CancellationToken ct);

    /// <summary>Сводка «Журнала прогнозов»: активные тревоги, ждущие решения, заведённые и кончившиеся за сутки.</summary>
    Task<PredictionStatsDto> StatsAsync(CancellationToken ct);

    /// <summary>canManage — у пользователя predictions:manage: без него Mute и Reopen заглушенного — 403.</summary>
    Task<PredictionDecisionDto> DecideAsync(
        Guid predictionId, Guid userId, CreatePredictionDecisionRequest request, bool canManage, CancellationToken ct);

    /// <summary>live=true — только живые эпизоды (FactAlertLiveness), false — только прошедшие.</summary>
    Task<PagedResult<FactAlertDto>> ListFactAlertsAsync(int? objectId, bool? live, int page, int pageSize, CancellationToken ct);
    Task<FactAlertDto> CreateFactAlertAsync(CreateFactAlertRequest request, CancellationToken ct);

    /// <summary>Эпизод по факту из tf.forecast.results. Объявление (announcement=true) — как
    /// CreateFactAlertAsync, но повтор (тот же объект, тип и начало эпизода) не создаёт второй записи.
    /// Обновление переписывает LastAt, маршрут и подробности у того же эпизода или у живого эпизода того же
    /// типа на объекте. Эпизод, которого BFF не видел (объявление пропущено), заводится — Created=true, чтобы
    /// уведомление всё же ушло.</summary>
    Task<(FactAlertDto Alert, bool Created)> RecordFactAlertAsync(
        CreateFactAlertRequest request, bool announcement, CancellationToken ct);
}

using BFF.Models.Enums;

namespace BFF.Contracts.Predictions;

public sealed class PredictionListItemDto
{
    public Guid Id { get; init; }
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public string Topic { get; init; } = string.Empty;
    public double Probability { get; init; }
    public DateTimeOffset HourEnd { get; init; }
    public int SinceHours { get; init; }
    public PredictionStatus Status { get; init; }

    /// <summary>Тревога модели ещё горит; false — кончилась в AlarmEndedAt.</summary>
    public bool Alarm { get; init; }
    public DateTimeOffset? AlarmEndedAt { get; init; }
}

public sealed class PredictionDto
{
    public Guid Id { get; init; }
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset HourEnd { get; init; }
    public short HorizonHours { get; init; }
    public double Score { get; init; }
    public double Threshold { get; init; }
    public bool Alarm { get; init; }
    public DateTimeOffset? AlarmEndedAt { get; init; }
    public double Probability { get; init; }
    public double Confidence { get; init; }
    public int SinceHours { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Classification { get; init; }
    public string? Recommendation { get; init; }
    public string? ModelVersionId { get; init; }
    public PredictionStatus Status { get; init; }
    public string? MutedReason { get; init; }

    /// <summary>До какого времени заглушена пара объект-тип — из последнего решения Mute; не заглушен — null.</summary>
    public DateTimeOffset? MutedUntil { get; init; }
    public IReadOnlyList<PredictionFactorDto> Factors { get; init; } = Array.Empty<PredictionFactorDto>();
    public IReadOnlyList<PredictionEvidenceDto> Evidence { get; init; } = Array.Empty<PredictionEvidenceDto>();
}

public sealed class PredictionFactorDto
{
    public string Feature { get; init; } = string.Empty;

    /// <summary>Подпись признака словами (reasons[].label модели); нет — показывают Feature.</summary>
    public string? Label { get; init; }

    public double Value { get; init; }
    public double Weight { get; init; }
    public string Direction { get; init; } = string.Empty;
}

public sealed class PredictionEvidenceDto
{
    public int SensorId { get; init; }
    public long? PicketId { get; init; }
    public DateTimeOffset Ts { get; init; }
    public double? Value { get; init; }

    /// <summary>Значение дискретного канала текстом («Обнаружен дым»), когда оно не число.</summary>
    public string? ValueText { get; init; }

    /// <summary>Из справочника датчиков и пикетов при чтении карточки; во входящем запросе не нужны.</summary>
    public string? SensorName { get; init; }
    public string? SensorType { get; init; }
    public string? PicketCode { get; init; }
}

/// <summary>Сводка «Журнала прогнозов» (GET /predictions/stats): сколько карточек и в каком состоянии.
/// Сверка с моделью: ActiveAlarms по типу ≈ last_tick.alarms_by_type из /api/ml/status (§9.8).</summary>
public sealed class PredictionStatsDto
{
    /// <summary>Последний час, за который модель прислала прогноз по карточке; нет карточек — null.</summary>
    public DateTimeOffset? LastHourEnd { get; init; }

    public int ActiveAlarms { get; init; }

    /// <summary>Тревога числится горящей, но модель не подтверждала её дольше часа после LastHourEnd —
    /// сообщения потерялись. В норме 0.</summary>
    public int StaleAlarms { get; init; }

    public int Open { get; init; }
    public int CreatedLast24h { get; init; }
    public int EndedLast24h { get; init; }
    public IReadOnlyList<PredictionTypeStatsDto> ByType { get; init; } = Array.Empty<PredictionTypeStatsDto>();
}

public sealed class PredictionTypeStatsDto
{
    public PredictionType Type { get; init; }

    /// <summary>Карточки, у которых тревога модели ещё горит (любой статус).</summary>
    public int ActiveAlarms { get; init; }

    /// <summary>Ждут решения диспетчера: New и InReview.</summary>
    public int Open { get; init; }

    public int Taken { get; init; }
    public int Muted { get; init; }
    public int Rejected { get; init; }
    public int CreatedLast24h { get; init; }
    public int EndedLast24h { get; init; }
}

/// <summary>Created by the future tf.forecast.results Kafka consumer (not yet built) or, for now,
/// manually via this endpoint under a service-account grant — see docs/DECISIONS.md.</summary>
public sealed class CreatePredictionRequest
{
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset HourEnd { get; init; }
    public short HorizonHours { get; init; } = 24;
    public double Score { get; init; }
    public double Threshold { get; init; }
    public bool Alarm { get; init; }
    public double Probability { get; init; }
    public double Confidence { get; init; }
    public int SinceHours { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Classification { get; init; }
    public string? Recommendation { get; init; }
    public string? ModelVersionId { get; init; }
    public IReadOnlyList<PredictionFactorDto>? Factors { get; init; }
    public IReadOnlyList<PredictionEvidenceDto>? Evidence { get; init; }
}

public sealed class PredictionDecisionDto
{
    public Guid Id { get; init; }
    public Guid PredictionId { get; init; }
    public Guid UserId { get; init; }
    public DecisionAction Action { get; init; }
    public string? ReasonCode { get; init; }
    public string? Comment { get; init; }
    public Guid? TaskId { get; init; }
    public DateTimeOffset? MutedUntil { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

public sealed class CreatePredictionDecisionRequest
{
    public DecisionAction Action { get; init; }
    public string? ReasonCode { get; init; }
    public string? Comment { get; init; }

    /// <summary>Take: прикрепить прогноз к уже открытой заявке (два типа на одном объекте — одна заявка,
    /// домен §4.2). Пусто — BFF заводит новую заявку и отдаёт её id в TaskId решения.</summary>
    public Guid? TaskId { get; init; }

    /// <summary>Mute: до какого времени молчит пара объект-тип (ML/INTEGRATION.md §13.3) — обязателен.</summary>
    public DateTimeOffset? Until { get; init; }
}

public sealed class FactAlertDto
{
    public Guid Id { get; init; }
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public AlertGroup Group { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset AnnouncedAt { get; init; }
    public DateTimeOffset LastAt { get; init; }

    /// <summary>Эпизод живой: модель сообщала о нём не раньше FactAlertLiveness.Window назад.</summary>
    public bool Live { get; init; }

    public IReadOnlyList<int> TriggerSensorIds { get; init; } = Array.Empty<int>();
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<FactRoutePointDto> Route { get; init; } = Array.Empty<FactRoutePointDto>();
    public string? DetailsJson { get; init; }
}

/// <summary>Точка маршрута нарушителя (§13.11): сработка датчика охраны. Координаты — у датчика в справочнике.</summary>
public sealed class FactRoutePointDto
{
    public int SensorId { get; init; }
    public string? SType { get; init; }
    public DateTimeOffset At { get; init; }
}

public sealed class CreateFactAlertRequest
{
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset AnnouncedAt { get; init; }

    /// <summary>Последнее время эпизода; без него — AnnouncedAt.</summary>
    public DateTimeOffset? LastAt { get; init; }

    public IReadOnlyList<int> TriggerSensorIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<FactRoutePointDto> Route { get; init; } = Array.Empty<FactRoutePointDto>();
    public string? DetailsJson { get; init; }
}

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
    public IReadOnlyList<PredictionFactorDto> Factors { get; init; } = Array.Empty<PredictionFactorDto>();
    public IReadOnlyList<PredictionEvidenceDto> Evidence { get; init; } = Array.Empty<PredictionEvidenceDto>();
}

public sealed class PredictionFactorDto
{
    public string Feature { get; init; } = string.Empty;
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
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset AnnouncedAt { get; init; }
    public IReadOnlyList<int> TriggerSensorIds { get; init; } = Array.Empty<int>();
    public string Status { get; init; } = string.Empty;
}

public sealed class CreateFactAlertRequest
{
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset AnnouncedAt { get; init; }
    public IReadOnlyList<int> TriggerSensorIds { get; init; } = Array.Empty<int>();
}

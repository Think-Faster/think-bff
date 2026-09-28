using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>"Already happening" channel — opposite of a Prediction, which is "may happen".</summary>
public sealed class FactAlert
{
    public Guid Id { get; set; }
    public int ObjectId { get; set; }
    public PredictionType Type { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset AnnouncedAt { get; set; }

    /// <summary>last_at последнего сообщения модели об этом эпизоде: эпизод живой, пока оно свежее.</summary>
    public DateTimeOffset LastAt { get; set; }

    public int[] TriggerSensorIds { get; set; } = Array.Empty<int>();
    public string Status { get; set; } = string.Empty;

    /// <summary>Маршрут нарушителя, jsonb <c>[{sensorId, stype, at}]</c> по времени — только у Intrusion.</summary>
    public string? RouteJson { get; set; }

    /// <summary>Подробности типа из сообщения модели, jsonb (§13.11): direction и channels у Temperature,
    /// cause, share и possibleAccident у Blind, temperature у Fire при жаре.</summary>
    public string? DetailsJson { get; set; }
}

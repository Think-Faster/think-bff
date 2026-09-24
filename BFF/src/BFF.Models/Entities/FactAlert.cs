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
    public int[] TriggerSensorIds { get; set; } = Array.Empty<int>();
    public string Status { get; set; } = string.Empty;
}

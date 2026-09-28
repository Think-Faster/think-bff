using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class PredictionDecision
{
    public Guid Id { get; set; }
    public Guid PredictionId { get; set; }
    public Guid UserId { get; set; }
    public DecisionAction Action { get; set; }
    public string? ReasonCode { get; set; }
    public string? Comment { get; set; }
    public Guid? TaskId { get; set; }
    /// <summary>До какого времени молчит пара объект-тип — только у Mute.</summary>
    public DateTimeOffset? MutedUntil { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

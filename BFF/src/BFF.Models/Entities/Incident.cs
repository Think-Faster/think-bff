using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class Incident
{
    public Guid Id { get; set; }
    public int ObjectId { get; set; }
    public PredictionType Type { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? PredictionId { get; set; }
    public string? Outcome { get; set; }
}

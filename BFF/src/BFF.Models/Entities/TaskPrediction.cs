namespace BFF.Models.Entities;

/// <summary>Attaches a Prediction as grounds for a WorkTask. Composite key (TaskId, PredictionId).</summary>
public sealed class TaskPrediction
{
    public Guid TaskId { get; set; }
    public Guid PredictionId { get; set; }
    public Guid AttachedBy { get; set; }
    public DateTimeOffset AttachedAt { get; set; }
    public DateTimeOffset? DetachedAt { get; set; }
    public bool IsPrimary { get; set; }
}

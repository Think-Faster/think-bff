namespace BFF.Models.Entities;

/// <summary>The request/tracking record only — the actual retraining computation runs in tf-model.</summary>
public sealed class RetrainJob
{
    public Guid Id { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public string? ParamsJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ResultModelVersionId { get; set; }
    public string? LogRef { get; set; }
}

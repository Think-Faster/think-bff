using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Named WorkTask, not Task — "task" collides with System.Threading.Tasks.Task.</summary>
public sealed class WorkTask
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public TaskSourceType SourceType { get; set; }
    public int ObjectId { get; set; }
    public long? PicketId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WorkType { get; set; }
    public string? FaultClassification { get; set; }
    public int[] SensorIds { get; set; } = Array.Empty<int>();
    public string? Comment { get; set; }
    public Guid? DispatcherId { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.New;
    public short Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

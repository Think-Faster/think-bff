namespace BFF.Models.Entities;

public sealed class TaskReturn
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid ReturnedBy { get; set; }
    public string TargetType { get; set; } = string.Empty; // "dispatcher" | "queue" | "incident"
    public Guid? TargetUserId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset ReturnedAt { get; set; }
}

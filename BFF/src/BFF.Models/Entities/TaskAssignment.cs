namespace BFF.Models.Entities;

public sealed class TaskAssignment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid EngineerId { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

namespace BFF.Models.Entities;

public sealed class TaskReport
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid EngineerId { get; set; }
    public string? ActualState { get; set; }
    public string? WorksDone { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

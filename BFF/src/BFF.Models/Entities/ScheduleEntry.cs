using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class ScheduleEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public ScheduleStatus Status { get; set; }
    public string? Source { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

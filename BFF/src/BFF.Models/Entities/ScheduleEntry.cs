using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class ScheduleEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public ScheduleStatus Status { get; set; }
    /// <summary>Начало смены в каждый день интервала; вместе с ShiftHours даёт часы работы (сутки через трое — 08:00 на 24 ч).</summary>
    public TimeOnly? ShiftStart { get; set; }
    public short? ShiftHours { get; set; }
    public string? Source { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

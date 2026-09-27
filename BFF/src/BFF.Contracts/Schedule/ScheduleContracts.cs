using BFF.Models.Enums;

namespace BFF.Contracts.Schedule;

public sealed class ScheduleEntryDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public ScheduleStatus Status { get; init; }
    public TimeOnly? ShiftStart { get; init; }
    public short? ShiftHours { get; init; }
    public string? Source { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
}

public sealed class CreateScheduleEntryRequest
{
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public ScheduleStatus Status { get; init; }
    public TimeOnly? ShiftStart { get; init; }
    public short? ShiftHours { get; init; }
    public string? Source { get; init; }
}

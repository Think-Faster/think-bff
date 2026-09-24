using BFF.Models.Enums;

namespace BFF.Contracts.Incidents;

public sealed class IncidentDto
{
    public Guid Id { get; init; }
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public Guid? ConfirmedBy { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? PredictionId { get; init; }
    public string? Outcome { get; init; }
}

public sealed class CreateIncidentRequest
{
    public int ObjectId { get; init; }
    public PredictionType Type { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? PredictionId { get; init; }
}

public sealed class ConfirmIncidentRequest
{
    public string? Outcome { get; init; }
}

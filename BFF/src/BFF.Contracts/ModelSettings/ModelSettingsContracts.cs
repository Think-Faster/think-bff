using BFF.Models.Enums;

namespace BFF.Contracts.ModelSettings;

public sealed class ModelVersionDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public DateTimeOffset? SwitchedAt { get; init; }
    public Guid? SwitchedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateModelVersionRequest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class CoefficientDto
{
    public Guid Id { get; init; }
    public PredictionType Type { get; init; }
    public double Share { get; init; }
    public double? RejectK { get; init; }
    public int Version { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? Reason { get; init; }
}

/// <summary>Every edit is a new version row, never an in-place update — see docs/DECISIONS.md.</summary>
public sealed class CreateCoefficientRequest
{
    public PredictionType Type { get; init; }
    public double Share { get; init; }
    public double? RejectK { get; init; }
    public string? Reason { get; init; }
}

public sealed class RetrainJobDto
{
    public Guid Id { get; init; }
    public Guid RequestedBy { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public string? ParamsJson { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? ResultModelVersionId { get; init; }
    public string? LogRef { get; init; }
}

public sealed class CreateRetrainJobRequest
{
    public string? ParamsJson { get; init; }
}

public sealed class IgnoredRangeDto
{
    public Guid Id { get; init; }
    public IgnoredRangeScope Scope { get; init; }
    public int? ObjectId { get; init; }
    public int? SensorId { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateIgnoredRangeRequest
{
    public IgnoredRangeScope Scope { get; init; }
    public int? ObjectId { get; init; }
    public int? SensorId { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Current version of a planned work. Every edit adds a version; delete adds a version with deleted = true.</summary>
public sealed class WorkScheduleEntryDto
{
    public long WorkId { get; init; }
    public int Version { get; init; }
    public int? ObjectId { get; init; }
    public string WorkKind { get; init; } = string.Empty;
    public IReadOnlyList<string> IncidentTypes { get; init; } = Array.Empty<string>();
    public string? RemovedSensor { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public WorkSource Source { get; init; }
    public string? Comment { get; init; }
    public Guid? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Body of both POST (version 1) and PUT (next version) on work-schedule.</summary>
public sealed class UpsertWorkScheduleEntryRequest
{
    public int? ObjectId { get; init; }
    public string WorkKind { get; init; } = string.Empty;
    public IReadOnlyList<string>? IncidentTypes { get; init; }
    public string? RemovedSensor { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public WorkSource Source { get; init; }
    public string? Comment { get; init; }
}

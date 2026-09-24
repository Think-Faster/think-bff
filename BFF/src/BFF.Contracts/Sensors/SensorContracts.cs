namespace BFF.Contracts.Sensors;

public sealed class SensorDto
{
    public int Id { get; init; }
    public int ObjectId { get; init; }
    public long? PicketId { get; init; }
    public string System { get; init; } = string.Empty;
    public string SType { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class CreateSensorRequest
{
    public int Id { get; init; }
    public int ObjectId { get; init; }
    public long? PicketId { get; init; }
    public string System { get; init; } = string.Empty;
    public string SType { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateSensorRequest
{
    public long? PicketId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SensorLinkDto
{
    public int FromSensorId { get; init; }
    public int ToSensorId { get; init; }
    public string Kind { get; init; } = string.Empty;
}

public sealed class CreateSensorLinkRequest
{
    public int ToSensorId { get; init; }
    public string Kind { get; init; } = string.Empty;
}

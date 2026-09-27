using BFF.Models.Enums;

namespace BFF.Contracts.Objects;

public sealed class ObjectDto
{
    public int Id { get; init; }
    public short Level { get; init; }
    public int? ParentId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? GeometryGeoJson { get; init; }
    public ObjectStatus Status { get; init; }
    public DateTimeOffset StatusAt { get; init; }
}

public sealed class CreateObjectRequest
{
    public int Id { get; init; }
    public short Level { get; init; }
    public int? ParentId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? GeometryGeoJson { get; init; }
}

public sealed class UpdateObjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? GeometryGeoJson { get; init; }
}

public sealed class PicketDto
{
    public long Id { get; init; }
    public int ObjectId { get; init; }
    public string Code { get; init; } = string.Empty;
    public int Ordinal { get; init; }
    public string? GeometryGeoJson { get; init; }
}

public sealed class CreatePicketRequest
{
    public string Code { get; init; } = string.Empty;
    public int Ordinal { get; init; }
    public string? GeometryGeoJson { get; init; }
}

public sealed class UpdatePicketRequest
{
    public int Ordinal { get; init; }
    public string? GeometryGeoJson { get; init; }
}

public sealed class MapLayerDto
{
    public Guid Id { get; init; }
    public short Level { get; init; }
    public int? ObjectId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string GeoJson { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertMapLayerRequest
{
    public short Level { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string GeoJson { get; init; } = string.Empty;
}

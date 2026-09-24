using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Named MonitoringObject, not Object — "object" collides with System.Object.</summary>
public sealed class MonitoringObject
{
    public int Id { get; set; }
    public short Level { get; set; }
    public int? ParentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }

    /// <summary>Raw GeoJSON geometry (point/polygon). No PostGIS/NetTopologySuite dependency for now —
    /// see docs/DECISIONS.md.</summary>
    public string? GeometryGeoJson { get; set; }

    public ObjectStatus Status { get; set; }
    public DateTimeOffset StatusAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

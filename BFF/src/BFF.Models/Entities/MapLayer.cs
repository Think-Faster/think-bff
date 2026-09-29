namespace BFF.Models.Entities;

/// <summary>Level: 1 city map, 2 object map, 3 sensor layer, 4 internal schema.</summary>
public sealed class MapLayer
{
    public Guid Id { get; set; }
    public short Level { get; set; }
    public int? ObjectId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string GeoJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

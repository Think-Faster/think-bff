namespace BFF.Models.Entities;

public sealed class Picket
{
    public long Id { get; set; }
    public int ObjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string? GeometryGeoJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

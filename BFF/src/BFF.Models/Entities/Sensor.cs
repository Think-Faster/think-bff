namespace BFF.Models.Entities;

/// <summary>Dictionary entry for a sensor. Live state (last value/state/timestamp) is owned by
/// tf-funnel, not stored here — see docs/DECISIONS.md.</summary>
public sealed class Sensor
{
    public int Id { get; set; }
    public int ObjectId { get; set; }
    public long? PicketId { get; set; }
    public string System { get; set; } = string.Empty;
    public string SType { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

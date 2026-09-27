namespace BFF.Models.Entities;

/// <summary>Condition line between two sensors, for the internal schema view.</summary>
public sealed class SensorLink
{
    public int FromSensorId { get; set; }
    public int ToSensorId { get; set; }
    public string Kind { get; set; } = string.Empty;
}

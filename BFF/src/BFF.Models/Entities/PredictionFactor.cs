namespace BFF.Models.Entities;

public sealed class PredictionFactor
{
    public Guid Id { get; set; }
    public Guid PredictionId { get; set; }
    public string Feature { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Weight { get; set; }
    public string Direction { get; set; } = string.Empty;
}

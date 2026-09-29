namespace BFF.Models.Entities;

public sealed class PredictionFactor
{
    public Guid Id { get; set; }
    public Guid PredictionId { get; set; }
    public string Feature { get; set; } = string.Empty;

    /// <summary>Подпись признака словами от модели (reasons[].label, §2.3); нет — показывают Feature.</summary>
    public string? Label { get; set; }

    public double Value { get; set; }
    public double Weight { get; set; }
    public string Direction { get; set; } = string.Empty;
}

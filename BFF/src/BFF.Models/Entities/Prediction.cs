using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class Prediction
{
    public Guid Id { get; set; }
    public int ObjectId { get; set; }
    public PredictionType Type { get; set; }
    public DateTimeOffset HourEnd { get; set; }
    public short HorizonHours { get; set; } = 24;
    public double Score { get; set; }
    public double Threshold { get; set; }
    public bool Alarm { get; set; }
    public double Probability { get; set; }
    public double Confidence { get; set; }
    public int SinceHours { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Classification { get; set; }
    public string? Recommendation { get; set; }
    public string? ModelVersionId { get; set; }
    public PredictionStatus Status { get; set; } = PredictionStatus.New;
    public string? MutedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<PredictionFactor> Factors { get; set; } = new();
    public List<PredictionEvidence> Evidence { get; set; } = new();
}

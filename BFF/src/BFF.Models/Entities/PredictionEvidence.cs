namespace BFF.Models.Entities;

public sealed class PredictionEvidence
{
    public Guid Id { get; set; }
    public Guid PredictionId { get; set; }
    public int SensorId { get; set; }
    public long? PicketId { get; set; }
    public DateTimeOffset Ts { get; set; }
    public double? Value { get; set; }
}

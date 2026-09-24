using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class IgnoredRange
{
    public Guid Id { get; set; }
    public IgnoredRangeScope Scope { get; set; }
    public int? ObjectId { get; set; }
    public int? SensorId { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

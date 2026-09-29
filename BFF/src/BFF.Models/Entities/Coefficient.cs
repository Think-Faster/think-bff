using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Versioned setting, not mutated in place — a new row per edit, so past traffic can be
/// explained by the coefficients active at the time.</summary>
public sealed class Coefficient
{
    public Guid Id { get; set; }
    public PredictionType Type { get; set; }
    public double Share { get; set; }
    public double? RejectK { get; set; }
    public int Version { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Reason { get; set; }
}

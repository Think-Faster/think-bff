namespace BFF.Models.Entities;

/// <summary>The admin-facing control side only (which version is active). The registry — metrics,
/// training data, registry_ref — stays with tf-model; see docs/DECISIONS.md.</summary>
public sealed class ModelVersion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTimeOffset? SwitchedAt { get; set; }
    public Guid? SwitchedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

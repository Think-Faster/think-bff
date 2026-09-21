namespace BFF.Models.Entities;

/// <summary>Materialized transitive closure of the group graph, including reflexive rows (g, g, 0).</summary>
public sealed class GroupClosure
{
    public Guid AncestorId { get; set; }
    public Guid DescendantId { get; set; }
    public int Depth { get; set; }
}

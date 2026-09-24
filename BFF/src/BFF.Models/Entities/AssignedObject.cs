namespace BFF.Models.Entities;

/// <summary>Ownership, not access: which object a dispatcher/technician is responsible for. Composite
/// key (UserId, ObjectId).</summary>
public sealed class AssignedObject
{
    public Guid UserId { get; set; }
    public int ObjectId { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public string? Note { get; set; }
}

namespace BFF.Models.Entities;

/// <summary>Single-row counter (id = 1) incremented in the same transaction as any RBAC mutation, used to invalidate the permission cache. Not listed in the original file map (section 3) but required by section 7.4.</summary>
public sealed class RbacVersion
{
    public int Id { get; set; }
    public long Value { get; set; }
}

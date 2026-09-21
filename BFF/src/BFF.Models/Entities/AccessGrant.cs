using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class AccessGrant
{
    public Guid Id { get; set; }
    public PrincipalType PrincipalType { get; set; }
    public Guid PrincipalId { get; set; }
    public int ResourceId { get; set; }
    public PermissionFlags PermissionMask { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Resource? Resource { get; set; }
}

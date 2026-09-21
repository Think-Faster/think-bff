using BFF.Models.Enums;

namespace BFF.Models.Entities;

/// <summary>Edge of the group composition graph. Composite key (GroupId, MemberType, MemberId).</summary>
public sealed class GroupMember
{
    public Guid GroupId { get; set; }
    public MemberType MemberType { get; set; }
    public Guid MemberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

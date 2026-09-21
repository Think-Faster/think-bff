using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members", tb =>
        {
            tb.HasCheckConstraint("ck_group_members_member_type", "member_type IN (1,2)");
            tb.HasCheckConstraint("ck_group_members_no_self_loop", "member_type <> 2 OR member_id <> group_id");
        });

        builder.HasKey(m => new { m.GroupId, m.MemberType, m.MemberId }).HasName("pk_group_members");

        builder.Property(m => m.GroupId).HasColumnName("group_id");
        builder.Property(m => m.MemberType).HasColumnName("member_type").HasConversion<short>();
        builder.Property(m => m.MemberId).HasColumnName("member_id");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(m => m.GroupId)
            .HasConstraintName("fk_group_members_groups")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.MemberType, m.MemberId })
            .HasDatabaseName("ix_group_members_member")
            .IncludeProperties(m => m.GroupId);
    }
}

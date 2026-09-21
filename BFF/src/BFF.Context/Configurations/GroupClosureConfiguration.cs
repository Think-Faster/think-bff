using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class GroupClosureConfiguration : IEntityTypeConfiguration<GroupClosure>
{
    public void Configure(EntityTypeBuilder<GroupClosure> builder)
    {
        builder.ToTable("group_closure");

        builder.HasKey(c => new { c.AncestorId, c.DescendantId }).HasName("pk_group_closure");

        builder.Property(c => c.AncestorId).HasColumnName("ancestor_id");
        builder.Property(c => c.DescendantId).HasColumnName("descendant_id");
        builder.Property(c => c.Depth).HasColumnName("depth");

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(c => c.AncestorId)
            .HasConstraintName("fk_group_closure_ancestor")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(c => c.DescendantId)
            .HasConstraintName("fk_group_closure_descendant")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.DescendantId, c.AncestorId })
            .HasDatabaseName("ix_group_closure_descendant_ancestor");
    }
}

using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class AssignedObjectConfiguration : IEntityTypeConfiguration<AssignedObject>
{
    public void Configure(EntityTypeBuilder<AssignedObject> builder)
    {
        builder.ToTable("assigned_objects");

        builder.HasKey(a => new { a.UserId, a.ObjectId }).HasName("pk_assigned_objects");

        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.ObjectId).HasColumnName("object_id");
        builder.Property(a => a.AssignedBy).HasColumnName("assigned_by");
        builder.Property(a => a.AssignedAt).HasColumnName("assigned_at");
        builder.Property(a => a.Note).HasColumnName("note");

        builder.HasIndex(a => a.ObjectId).HasDatabaseName("ix_assigned_objects_object_id");
    }
}

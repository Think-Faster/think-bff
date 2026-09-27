using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("access_grants", tb =>
            tb.HasCheckConstraint("ck_access_grants_principal_type", "principal_type IN (1,2)"));

        builder.HasKey(a => a.Id).HasName("pk_access_grants");
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.PrincipalType).HasColumnName("principal_type").HasConversion<short>();
        builder.Property(a => a.PrincipalId).HasColumnName("principal_id");
        builder.Property(a => a.ResourceId).HasColumnName("resource_id");
        builder.Property(a => a.PermissionMask).HasColumnName("permission_mask").HasConversion<int>();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(a => a.Resource)
            .WithMany()
            .HasForeignKey(a => a.ResourceId)
            .HasConstraintName("fk_access_grants_resources")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.PrincipalType, a.PrincipalId, a.ResourceId })
            .IsUnique()
            .HasDatabaseName("ux_access_grants_principal_resource");

        builder.HasIndex(a => new { a.PrincipalType, a.PrincipalId })
            .HasDatabaseName("ix_access_grants_principal")
            .IncludeProperties(a => new { a.ResourceId, a.PermissionMask });
    }
}

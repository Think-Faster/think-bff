using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class RbacVersionConfiguration : IEntityTypeConfiguration<RbacVersion>
{
    public void Configure(EntityTypeBuilder<RbacVersion> builder)
    {
        builder.ToTable("rbac_version", tb =>
            tb.HasCheckConstraint("ck_rbac_version_singleton", "id = 1"));

        builder.HasKey(v => v.Id).HasName("pk_rbac_version");
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.Value).HasColumnName("value");
    }
}

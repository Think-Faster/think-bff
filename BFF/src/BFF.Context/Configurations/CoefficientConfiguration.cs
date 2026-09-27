using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class CoefficientConfiguration : IEntityTypeConfiguration<Coefficient>
{
    public void Configure(EntityTypeBuilder<Coefficient> builder)
    {
        builder.ToTable("coefficients");

        builder.HasKey(c => c.Id).HasName("pk_coefficients");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Type).HasColumnName("type").HasConversion<short>();
        builder.Property(c => c.Share).HasColumnName("share");
        builder.Property(c => c.RejectK).HasColumnName("reject_k");
        builder.Property(c => c.Version).HasColumnName("version");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.Reason).HasColumnName("reason");

        builder.HasIndex(c => new { c.Type, c.Version }).IsUnique().HasDatabaseName("ux_coefficients_type_version");
    }
}

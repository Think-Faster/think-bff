using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class IgnoredRangeConfiguration : IEntityTypeConfiguration<IgnoredRange>
{
    public void Configure(EntityTypeBuilder<IgnoredRange> builder)
    {
        builder.ToTable("ignored_ranges");

        builder.HasKey(r => r.Id).HasName("pk_ignored_ranges");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.Scope).HasColumnName("scope").HasConversion<short>();
        builder.Property(r => r.ObjectId).HasColumnName("object_id");
        builder.Property(r => r.SensorId).HasColumnName("sensor_id");
        builder.Property(r => r.DateFrom).HasColumnName("date_from");
        builder.Property(r => r.DateTo).HasColumnName("date_to");
        builder.Property(r => r.Reason).HasColumnName("reason").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(r => r.Scope).HasDatabaseName("ix_ignored_ranges_scope");
    }
}

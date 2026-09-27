using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class MonitoringObjectConfiguration : IEntityTypeConfiguration<MonitoringObject>
{
    public void Configure(EntityTypeBuilder<MonitoringObject> builder)
    {
        builder.ToTable("objects");

        builder.HasKey(o => o.Id).HasName("pk_objects");
        // External id from the monitoring system's own dictionary — the app never generates it.
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(o => o.Level).HasColumnName("level");
        builder.Property(o => o.ParentId).HasColumnName("parent_id");
        builder.Property(o => o.Kind).HasColumnName("kind").IsRequired();
        builder.Property(o => o.Name).HasColumnName("name").IsRequired();
        builder.Property(o => o.Address).HasColumnName("address");
        builder.Property(o => o.GeometryGeoJson).HasColumnName("geometry_geojson");
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(o => o.StatusAt).HasColumnName("status_at");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.ParentId).HasDatabaseName("ix_objects_parent_id");
    }
}

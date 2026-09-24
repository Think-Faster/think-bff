using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("sensors");

        builder.HasKey(s => s.Id).HasName("pk_sensors");
        // External "ид_канала_данных" — app never generates it.
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.ObjectId).HasColumnName("object_id");
        builder.Property(s => s.PicketId).HasColumnName("picket_id");
        builder.Property(s => s.System).HasColumnName("system").IsRequired();
        builder.Property(s => s.SType).HasColumnName("stype").IsRequired();
        builder.Property(s => s.Tag).HasColumnName("tag");
        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.ObjectId).HasDatabaseName("ix_sensors_object_id");
        builder.HasIndex(s => s.PicketId).HasDatabaseName("ix_sensors_picket_id");
    }
}

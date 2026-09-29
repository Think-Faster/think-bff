using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class SensorLinkConfiguration : IEntityTypeConfiguration<SensorLink>
{
    public void Configure(EntityTypeBuilder<SensorLink> builder)
    {
        builder.ToTable("sensor_links");

        builder.HasKey(l => new { l.FromSensorId, l.ToSensorId, l.Kind }).HasName("pk_sensor_links");

        builder.Property(l => l.FromSensorId).HasColumnName("from_sensor_id");
        builder.Property(l => l.ToSensorId).HasColumnName("to_sensor_id");
        builder.Property(l => l.Kind).HasColumnName("kind").IsRequired();
    }
}

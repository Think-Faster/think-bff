using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class FactAlertConfiguration : IEntityTypeConfiguration<FactAlert>
{
    public void Configure(EntityTypeBuilder<FactAlert> builder)
    {
        builder.ToTable("fact_alerts");

        builder.HasKey(a => a.Id).HasName("pk_fact_alerts");
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.ObjectId).HasColumnName("object_id");
        builder.Property(a => a.Type).HasColumnName("type").HasConversion<short>();
        builder.Property(a => a.StartedAt).HasColumnName("started_at");
        builder.Property(a => a.AnnouncedAt).HasColumnName("announced_at");
        builder.Property(a => a.TriggerSensorIds).HasColumnName("trigger_sensor_ids");
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();

        builder.HasIndex(a => new { a.ObjectId, a.Type }).HasDatabaseName("ix_fact_alerts_object_type");
    }
}

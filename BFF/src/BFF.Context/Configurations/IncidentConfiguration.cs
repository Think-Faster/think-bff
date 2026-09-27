using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(i => i.Id).HasName("pk_incidents");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.ObjectId).HasColumnName("object_id");
        builder.Property(i => i.Type).HasColumnName("type").HasConversion<short>();
        builder.Property(i => i.StartedAt).HasColumnName("started_at");
        builder.Property(i => i.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(i => i.ConfirmedBy).HasColumnName("confirmed_by");
        builder.Property(i => i.TaskId).HasColumnName("task_id");
        builder.Property(i => i.PredictionId).HasColumnName("prediction_id");
        builder.Property(i => i.Outcome).HasColumnName("outcome");

        builder.HasIndex(i => i.ObjectId).HasDatabaseName("ix_incidents_object_id");
    }
}

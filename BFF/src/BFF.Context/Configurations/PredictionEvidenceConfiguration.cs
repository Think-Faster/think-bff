using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class PredictionEvidenceConfiguration : IEntityTypeConfiguration<PredictionEvidence>
{
    public void Configure(EntityTypeBuilder<PredictionEvidence> builder)
    {
        builder.ToTable("prediction_evidence");

        builder.HasKey(e => e.Id).HasName("pk_prediction_evidence");
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.PredictionId).HasColumnName("prediction_id");
        builder.Property(e => e.SensorId).HasColumnName("sensor_id");
        builder.Property(e => e.PicketId).HasColumnName("picket_id");
        builder.Property(e => e.Ts).HasColumnName("ts");
        builder.Property(e => e.Value).HasColumnName("value");

        builder.HasIndex(e => e.PredictionId).HasDatabaseName("ix_prediction_evidence_prediction_id");
    }
}

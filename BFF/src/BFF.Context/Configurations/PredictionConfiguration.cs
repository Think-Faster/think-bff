using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> builder)
    {
        builder.ToTable("predictions");

        builder.HasKey(p => p.Id).HasName("pk_predictions");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.ObjectId).HasColumnName("object_id");
        builder.Property(p => p.Type).HasColumnName("type").HasConversion<short>();
        builder.Property(p => p.HourEnd).HasColumnName("hour_end");
        builder.Property(p => p.HorizonHours).HasColumnName("horizon_hours");
        builder.Property(p => p.Score).HasColumnName("score");
        builder.Property(p => p.Threshold).HasColumnName("threshold");
        builder.Property(p => p.Alarm).HasColumnName("alarm");
        builder.Property(p => p.Probability).HasColumnName("probability");
        builder.Property(p => p.Confidence).HasColumnName("confidence");
        builder.Property(p => p.SinceHours).HasColumnName("since_hours");
        builder.Property(p => p.Topic).HasColumnName("topic").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.Classification).HasColumnName("classification");
        builder.Property(p => p.Recommendation).HasColumnName("recommendation");
        builder.Property(p => p.ModelVersionId).HasColumnName("model_version_id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(p => p.MutedReason).HasColumnName("muted_reason");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        // Dedup key from the source spec: one row per (object, type, hour, model version).
        builder.HasIndex(p => new { p.ObjectId, p.Type, p.HourEnd, p.ModelVersionId })
            .IsUnique()
            .HasDatabaseName("ux_predictions_dedup");
        builder.HasIndex(p => new { p.ObjectId, p.Status }).HasDatabaseName("ix_predictions_object_status");

        builder.HasMany(p => p.Factors).WithOne().HasForeignKey(f => f.PredictionId)
            .HasConstraintName("fk_prediction_factors_predictions").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Evidence).WithOne().HasForeignKey(e => e.PredictionId)
            .HasConstraintName("fk_prediction_evidence_predictions").OnDelete(DeleteBehavior.Cascade);
    }
}

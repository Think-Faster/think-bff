using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class PredictionDecisionConfiguration : IEntityTypeConfiguration<PredictionDecision>
{
    public void Configure(EntityTypeBuilder<PredictionDecision> builder)
    {
        builder.ToTable("prediction_decisions");

        builder.HasKey(d => d.Id).HasName("pk_prediction_decisions");
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.PredictionId).HasColumnName("prediction_id");
        builder.Property(d => d.UserId).HasColumnName("user_id");
        builder.Property(d => d.Action).HasColumnName("action").HasConversion<short>();
        builder.Property(d => d.ReasonCode).HasColumnName("reason_code");
        builder.Property(d => d.Comment).HasColumnName("comment");
        builder.Property(d => d.TaskId).HasColumnName("task_id");
        builder.Property(d => d.MutedUntil).HasColumnName("muted_until");
        builder.Property(d => d.DecidedAt).HasColumnName("decided_at");

        builder.HasOne<Prediction>().WithMany().HasForeignKey(d => d.PredictionId)
            .HasConstraintName("fk_prediction_decisions_predictions").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.PredictionId).HasDatabaseName("ix_prediction_decisions_prediction_id");
    }
}

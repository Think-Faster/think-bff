using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class TaskPredictionConfiguration : IEntityTypeConfiguration<TaskPrediction>
{
    public void Configure(EntityTypeBuilder<TaskPrediction> builder)
    {
        builder.ToTable("task_predictions");

        builder.HasKey(tp => new { tp.TaskId, tp.PredictionId }).HasName("pk_task_predictions");

        builder.Property(tp => tp.TaskId).HasColumnName("task_id");
        builder.Property(tp => tp.PredictionId).HasColumnName("prediction_id");
        builder.Property(tp => tp.AttachedBy).HasColumnName("attached_by");
        builder.Property(tp => tp.AttachedAt).HasColumnName("attached_at");
        builder.Property(tp => tp.DetachedAt).HasColumnName("detached_at");
        builder.Property(tp => tp.IsPrimary).HasColumnName("is_primary");

        builder.HasOne<WorkTask>().WithMany().HasForeignKey(tp => tp.TaskId)
            .HasConstraintName("fk_task_predictions_tasks").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Prediction>().WithMany().HasForeignKey(tp => tp.PredictionId)
            .HasConstraintName("fk_task_predictions_predictions").OnDelete(DeleteBehavior.Cascade);
    }
}

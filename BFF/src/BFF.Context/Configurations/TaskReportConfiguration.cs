using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class TaskReportConfiguration : IEntityTypeConfiguration<TaskReport>
{
    public void Configure(EntityTypeBuilder<TaskReport> builder)
    {
        builder.ToTable("task_reports");

        builder.HasKey(r => r.Id).HasName("pk_task_reports");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TaskId).HasColumnName("task_id");
        builder.Property(r => r.EngineerId).HasColumnName("engineer_id");
        builder.Property(r => r.ActualState).HasColumnName("actual_state");
        builder.Property(r => r.WorksDone).HasColumnName("works_done");
        builder.Property(r => r.ResultCode).HasColumnName("result_code").IsRequired();
        builder.Property(r => r.Comment).HasColumnName("comment");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasOne<WorkTask>().WithMany().HasForeignKey(r => r.TaskId)
            .HasConstraintName("fk_task_reports_tasks").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TaskId).HasDatabaseName("ix_task_reports_task_id");
    }
}

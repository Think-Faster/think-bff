using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("task_assignments");

        builder.HasKey(a => a.Id).HasName("pk_task_assignments");
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.TaskId).HasColumnName("task_id");
        builder.Property(a => a.EngineerId).HasColumnName("engineer_id");
        builder.Property(a => a.AssignedBy).HasColumnName("assigned_by");
        builder.Property(a => a.AssignedAt).HasColumnName("assigned_at");
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.Comment).HasColumnName("comment");

        builder.HasOne<WorkTask>().WithMany().HasForeignKey(a => a.TaskId)
            .HasConstraintName("fk_task_assignments_tasks").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.TaskId).HasDatabaseName("ix_task_assignments_task_id");
        builder.HasIndex(a => a.EngineerId).HasDatabaseName("ix_task_assignments_engineer_id");
    }
}

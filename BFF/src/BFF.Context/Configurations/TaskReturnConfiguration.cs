using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class TaskReturnConfiguration : IEntityTypeConfiguration<TaskReturn>
{
    public void Configure(EntityTypeBuilder<TaskReturn> builder)
    {
        builder.ToTable("task_returns");

        builder.HasKey(r => r.Id).HasName("pk_task_returns");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TaskId).HasColumnName("task_id");
        builder.Property(r => r.ReturnedBy).HasColumnName("returned_by");
        builder.Property(r => r.TargetType).HasColumnName("target_type").IsRequired();
        builder.Property(r => r.TargetUserId).HasColumnName("target_user_id");
        builder.Property(r => r.Comment).HasColumnName("comment");
        builder.Property(r => r.ReturnedAt).HasColumnName("returned_at");

        builder.HasOne<WorkTask>().WithMany().HasForeignKey(r => r.TaskId)
            .HasConstraintName("fk_task_returns_tasks").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TaskId).HasDatabaseName("ix_task_returns_task_id");
    }
}

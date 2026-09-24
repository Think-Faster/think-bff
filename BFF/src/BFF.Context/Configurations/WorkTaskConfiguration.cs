using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("tasks");

        builder.HasKey(t => t.Id).HasName("pk_tasks");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.Number).HasColumnName("number").IsRequired();
        builder.Property(t => t.SourceType).HasColumnName("source_type").HasConversion<short>();
        builder.Property(t => t.ObjectId).HasColumnName("object_id");
        builder.Property(t => t.PicketId).HasColumnName("picket_id");
        builder.Property(t => t.Topic).HasColumnName("topic").IsRequired();
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.WorkType).HasColumnName("work_type");
        builder.Property(t => t.FaultClassification).HasColumnName("fault_classification");
        builder.Property(t => t.SensorIds).HasColumnName("sensor_ids");
        builder.Property(t => t.Comment).HasColumnName("comment");
        builder.Property(t => t.DispatcherId).HasColumnName("dispatcher_id");
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(t => t.Priority).HasColumnName("priority");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.TakenAt).HasColumnName("taken_at");
        builder.Property(t => t.AssignedAt).HasColumnName("assigned_at");
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");
        builder.Property(t => t.ClosedAt).HasColumnName("closed_at");

        builder.HasIndex(t => t.Number).IsUnique().HasDatabaseName("ux_tasks_number");
        builder.HasIndex(t => new { t.ObjectId, t.Status }).HasDatabaseName("ix_tasks_object_status");
        builder.HasIndex(t => t.DispatcherId).HasDatabaseName("ix_tasks_dispatcher_id");
    }
}

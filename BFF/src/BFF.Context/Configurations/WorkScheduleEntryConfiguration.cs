using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class WorkScheduleEntryConfiguration : IEntityTypeConfiguration<WorkScheduleEntry>
{
    public void Configure(EntityTypeBuilder<WorkScheduleEntry> builder)
    {
        builder.ToTable("work_schedule", tb =>
        {
            tb.HasCheckConstraint("ck_work_schedule_range", "ends_at > starts_at");
            tb.HasCheckConstraint("ck_work_schedule_source", "source IN (1,2,3)");
        });

        builder.HasKey(w => new { w.WorkId, w.Version }).HasName("pk_work_schedule");
        // Integer id shared with the model: works_2026.csv, prediction muted_work_id and settings.works use it.
        builder.Property(w => w.WorkId).HasColumnName("work_id")
            .HasDefaultValueSql("nextval('work_schedule_work_id_seq')");
        builder.Property(w => w.Version).HasColumnName("version").ValueGeneratedNever();

        builder.Property(w => w.ObjectId).HasColumnName("object_id");
        builder.Property(w => w.WorkKind).HasColumnName("work_kind").IsRequired();
        builder.Property(w => w.IncidentTypes).HasColumnName("incident_types");
        builder.Property(w => w.RemovedSensor).HasColumnName("removed_sensor");
        builder.Property(w => w.StartsAt).HasColumnName("starts_at");
        builder.Property(w => w.EndsAt).HasColumnName("ends_at");
        builder.Property(w => w.Source).HasColumnName("source").HasConversion<short>();
        builder.Property(w => w.Comment).HasColumnName("comment");
        builder.Property(w => w.Deleted).HasColumnName("deleted");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(w => new { w.ObjectId, w.StartsAt }).HasDatabaseName("ix_work_schedule_object_starts");
    }
}

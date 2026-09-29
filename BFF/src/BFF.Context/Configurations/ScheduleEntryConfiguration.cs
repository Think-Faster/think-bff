using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class ScheduleEntryConfiguration : IEntityTypeConfiguration<ScheduleEntry>
{
    public void Configure(EntityTypeBuilder<ScheduleEntry> builder)
    {
        builder.ToTable("schedule_entries", tb =>
            tb.HasCheckConstraint("ck_schedule_entries_shift_hours", "shift_hours IS NULL OR shift_hours BETWEEN 1 AND 24"));

        builder.HasKey(s => s.Id).HasName("pk_schedule_entries");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.DateFrom).HasColumnName("date_from");
        builder.Property(s => s.DateTo).HasColumnName("date_to");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(s => s.ShiftStart).HasColumnName("shift_start");
        builder.Property(s => s.ShiftHours).HasColumnName("shift_hours");
        builder.Property(s => s.Source).HasColumnName("source");
        builder.Property(s => s.ChangedBy).HasColumnName("changed_by");
        builder.Property(s => s.ChangedAt).HasColumnName("changed_at");

        builder.HasIndex(s => new { s.UserId, s.DateFrom, s.DateTo }).HasDatabaseName("ix_schedule_entries_user_range");
    }
}

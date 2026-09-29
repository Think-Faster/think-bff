using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class EngineerPermitConfiguration : IEntityTypeConfiguration<EngineerPermit>
{
    public void Configure(EntityTypeBuilder<EngineerPermit> builder)
    {
        builder.ToTable("engineer_permits", tb =>
        {
            tb.HasCheckConstraint("ck_engineer_permits_kind", "kind IN (1,2,3)");
            tb.HasCheckConstraint("ck_engineer_permits_level",
                "(kind = 1 AND level BETWEEN 1 AND 3) OR (kind = 2 AND level IS NULL) OR (kind = 3 AND level BETWEEN 2 AND 5)");
        });

        builder.HasKey(p => p.Id).HasName("pk_engineer_permits");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.Kind).HasColumnName("kind").HasConversion<short>();
        builder.Property(p => p.Level).HasColumnName("level");
        builder.Property(p => p.ValidUntil).HasColumnName("valid_until");
        builder.Property(p => p.DocumentNo).HasColumnName("document_no");
        builder.Property(p => p.CheckedBy).HasColumnName("checked_by");
        builder.Property(p => p.CheckedAt).HasColumnName("checked_at");

        builder.HasIndex(p => new { p.UserId, p.Kind }).HasDatabaseName("ix_engineer_permits_user_kind");
    }
}

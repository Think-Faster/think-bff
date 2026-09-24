using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class EngineerProfileConfiguration : IEntityTypeConfiguration<EngineerProfile>
{
    public void Configure(EntityTypeBuilder<EngineerProfile> builder)
    {
        builder.ToTable("engineer_profiles");

        builder.HasKey(p => p.UserId).HasName("pk_engineer_profiles");
        builder.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();

        builder.Property(p => p.BrigadeId).HasColumnName("brigade_id");
        builder.Property(p => p.Phone).HasColumnName("phone");
        builder.Property(p => p.Telegram).HasColumnName("telegram");
        builder.Property(p => p.Specialization).HasColumnName("specialization");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<short>();

        builder.HasOne<Brigade>().WithMany().HasForeignKey(p => p.BrigadeId)
            .HasConstraintName("fk_engineer_profiles_brigades").OnDelete(DeleteBehavior.SetNull);
    }
}

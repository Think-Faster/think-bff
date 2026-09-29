using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.ToTable("user_activity");

        builder.HasKey(a => a.UserId).HasName("pk_user_activity");
        builder.Property(a => a.UserId).HasColumnName("user_id").ValueGeneratedNever();

        builder.Property(a => a.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(a => a.LastAction).HasColumnName("last_action");
    }
}

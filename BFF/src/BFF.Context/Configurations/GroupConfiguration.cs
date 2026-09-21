using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.Id).HasName("pk_groups");
        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(g => g.Code).HasColumnName("code").IsRequired();
        builder.Property(g => g.Name).HasColumnName("name").IsRequired();
        builder.Property(g => g.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(g => g.Code).IsUnique().HasDatabaseName("ux_groups_code");
    }
}

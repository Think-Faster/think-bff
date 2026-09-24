using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> builder)
    {
        builder.ToTable("model_versions");

        builder.HasKey(v => v.Id).HasName("pk_model_versions");
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.Name).HasColumnName("name").IsRequired();
        builder.Property(v => v.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
        builder.Property(v => v.SwitchedAt).HasColumnName("switched_at");
        builder.Property(v => v.SwitchedBy).HasColumnName("switched_by");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
    }
}

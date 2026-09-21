using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(r => r.Id).HasName("pk_resources");
        builder.Property(r => r.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        builder.Property(r => r.Code).HasColumnName("code").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").IsRequired();

        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("ux_resources_code");
    }
}

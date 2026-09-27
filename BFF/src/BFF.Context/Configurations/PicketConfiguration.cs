using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class PicketConfiguration : IEntityTypeConfiguration<Picket>
{
    public void Configure(EntityTypeBuilder<Picket> builder)
    {
        builder.ToTable("pickets");

        builder.HasKey(p => p.Id).HasName("pk_pickets");
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(p => p.ObjectId).HasColumnName("object_id");
        builder.Property(p => p.Code).HasColumnName("code").IsRequired();
        builder.Property(p => p.Ordinal).HasColumnName("ordinal");
        builder.Property(p => p.GeometryGeoJson).HasColumnName("geometry_geojson");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => new { p.ObjectId, p.Code }).IsUnique().HasDatabaseName("ux_pickets_object_code");
    }
}

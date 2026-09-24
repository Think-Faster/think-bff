using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class MapLayerConfiguration : IEntityTypeConfiguration<MapLayer>
{
    public void Configure(EntityTypeBuilder<MapLayer> builder)
    {
        builder.ToTable("map_layers");

        builder.HasKey(m => m.Id).HasName("pk_map_layers");
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.Level).HasColumnName("level");
        builder.Property(m => m.ObjectId).HasColumnName("object_id");
        builder.Property(m => m.Kind).HasColumnName("kind").IsRequired();
        builder.Property(m => m.GeoJson).HasColumnName("geojson").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => new { m.ObjectId, m.Level }).HasDatabaseName("ix_map_layers_object_level");
    }
}

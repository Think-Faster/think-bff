using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class PredictionFactorConfiguration : IEntityTypeConfiguration<PredictionFactor>
{
    public void Configure(EntityTypeBuilder<PredictionFactor> builder)
    {
        builder.ToTable("prediction_factors");

        builder.HasKey(f => f.Id).HasName("pk_prediction_factors");
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.PredictionId).HasColumnName("prediction_id");
        builder.Property(f => f.Feature).HasColumnName("feature").IsRequired();
        builder.Property(f => f.Value).HasColumnName("value");
        builder.Property(f => f.Weight).HasColumnName("weight");
        builder.Property(f => f.Direction).HasColumnName("direction").IsRequired();

        builder.HasIndex(f => f.PredictionId).HasDatabaseName("ix_prediction_factors_prediction_id");
    }
}

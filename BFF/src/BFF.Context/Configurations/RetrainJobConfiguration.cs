using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class RetrainJobConfiguration : IEntityTypeConfiguration<RetrainJob>
{
    public void Configure(EntityTypeBuilder<RetrainJob> builder)
    {
        builder.ToTable("retrain_jobs");

        builder.HasKey(j => j.Id).HasName("pk_retrain_jobs");
        builder.Property(j => j.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(j => j.RequestedBy).HasColumnName("requested_by");
        builder.Property(j => j.RequestedAt).HasColumnName("requested_at");
        builder.Property(j => j.ParamsJson).HasColumnName("params").HasColumnType("jsonb");
        builder.Property(j => j.Status).HasColumnName("status").IsRequired();
        builder.Property(j => j.StartedAt).HasColumnName("started_at");
        builder.Property(j => j.FinishedAt).HasColumnName("finished_at");
        builder.Property(j => j.ResultModelVersionId).HasColumnName("result_model_version_id");
        builder.Property(j => j.LogRef).HasColumnName("log_ref");
    }
}

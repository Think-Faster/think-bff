using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFF.Context.Configurations;

public sealed class BrigadeConfiguration : IEntityTypeConfiguration<Brigade>
{
    public void Configure(EntityTypeBuilder<Brigade> builder)
    {
        builder.ToTable("brigades");

        builder.HasKey(b => b.Id).HasName("pk_brigades");
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Unit).HasColumnName("unit");
        builder.Property(b => b.LeaderId).HasColumnName("leader_id");
    }
}

using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearningApp.Api.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogins", "learning");
        builder.HasKey(x => x.ExternalLoginId);
        builder.Property(x => x.ExternalLoginId).ValueGeneratedNever();
        builder.Property(x => x.Issuer).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique().HasDatabaseName("UX_ExternalLogins_Issuer_Subject");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

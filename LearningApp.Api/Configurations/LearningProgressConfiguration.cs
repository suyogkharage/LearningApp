using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearningApp.Api.Configurations;

public sealed class LearningProgressConfiguration : IEntityTypeConfiguration<LearningProgress>
{
    public void Configure(EntityTypeBuilder<LearningProgress> builder)
    {
        builder.ToTable("LearningProgress", "learning");
        builder.HasKey(x => x.LearningProgressId);
        builder.Property(x => x.LearningProgressId).ValueGeneratedNever();
        builder.Property(x => x.PercentComplete).HasColumnType("decimal(5,2)").HasDefaultValue(0);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.UpdatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(x => x.Enrollment).WithOne(x => x.LearningProgress).HasForeignKey<LearningProgress>(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.EnrollmentId).IsUnique().HasDatabaseName("UX_LearningProgress_EnrollmentId");
        builder.ToTable(t => t.HasCheckConstraint("CK_LearningProgress_PercentComplete", "[PercentComplete] >= 0 AND [PercentComplete] <= 100"));
    }
}

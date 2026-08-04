using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearningApp.Api.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", "learning");
        builder.HasKey(x => x.CourseId);
        builder.Property(x => x.CourseId).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.DifficultyLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DurationHours).IsRequired();
        builder.Property(x => x.IsPublished).HasDefaultValue(false);
        builder.Property(x => x.CreatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedUtc);
        builder.HasOne(x => x.Category).WithMany(x => x.Courses).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CategoryId).HasDatabaseName("IX_Courses_CategoryId");
        builder.HasIndex(x => x.IsPublished).HasDatabaseName("IX_Courses_IsPublished");
        builder.ToTable(t => t.HasCheckConstraint("CK_Courses_DurationHours", "[DurationHours] > 0"));
    }
}

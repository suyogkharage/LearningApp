using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearningApp.Api.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments", "learning");
        builder.HasKey(x => x.EnrollmentId);
        builder.Property(x => x.EnrollmentId).ValueGeneratedNever();
        builder.Property(x => x.EnrolledUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Active");
        builder.HasOne(x => x.User).WithMany(x => x.Enrollments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany(x => x.Enrollments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique().HasDatabaseName("UX_Enrollments_UserId_CourseId");
        builder.HasIndex(x => x.CourseId).HasDatabaseName("IX_Enrollments_CourseId");
    }
}

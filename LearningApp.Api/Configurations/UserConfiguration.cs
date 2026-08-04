using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearningApp.Api.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "learning");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.Property(x => x.Email).HasColumnName("Email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(150).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(512).IsRequired();
        builder.Property(x => x.Role).HasColumnName("Role").HasMaxLength(50).IsRequired().HasDefaultValue("Learner");
        builder.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        builder.Property(x => x.CreatedUtc).HasColumnName("CreatedUtc").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedUtc).HasColumnName("UpdatedUtc");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email");
    }
}

using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Data;

public sealed class LearningAppDbContext(DbContextOptions<LearningAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LearningProgress> LearningProgress => Set<LearningProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningAppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

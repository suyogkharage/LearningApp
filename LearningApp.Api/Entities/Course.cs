namespace LearningApp.Api.Entities;

public sealed class Course
{
    public Guid CourseId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Beginner";
    public int DurationHours { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public Category Category { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

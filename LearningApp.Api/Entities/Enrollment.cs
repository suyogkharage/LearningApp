namespace LearningApp.Api.Entities;

public sealed class Enrollment
{
    public Guid EnrollmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime EnrolledUtc { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? CompletedUtc { get; set; }
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public LearningProgress? LearningProgress { get; set; }
}

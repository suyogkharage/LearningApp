namespace LearningApp.Api.Entities;

public sealed class LearningProgress
{
    public Guid LearningProgressId { get; set; }
    public Guid EnrollmentId { get; set; }
    public decimal PercentComplete { get; set; }
    public DateTime? LastAccessedUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
}

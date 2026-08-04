namespace LearningApp.Api.Entities;

public sealed class Category
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

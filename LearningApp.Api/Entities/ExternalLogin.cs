namespace LearningApp.Api.Entities;

public sealed class ExternalLogin
{
    public Guid ExternalLoginId { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

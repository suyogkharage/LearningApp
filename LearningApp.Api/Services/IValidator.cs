namespace LearningApp.Api.Services;

public interface IValidator<in TRequest>
{
    IReadOnlyCollection<string> Validate(TRequest request);
}

using System.ComponentModel.DataAnnotations;

namespace LearningApp.Api.Services;

public sealed class DataAnnotationsValidator<TRequest> : IValidator<TRequest>
{
    public IReadOnlyCollection<string> Validate(TRequest request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request!);
        Validator.TryValidateObject(request!, context, results, validateAllProperties: true);
        return results.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray();
    }
}

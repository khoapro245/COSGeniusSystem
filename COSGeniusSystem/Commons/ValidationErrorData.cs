using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace COSGeniusSystem.Commons;

public sealed record ValidationErrorData(IReadOnlyDictionary<string, string[]> Errors)
{
    public static ValidationErrorData FromModelState(ModelStateDictionary modelState) => new(
        modelState.Where(entry => entry.Value?.Errors.Count > 0).ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "The supplied value is invalid." : error.ErrorMessage).ToArray()));
}

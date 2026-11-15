using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace COSGeniusSystem.Commons;

public sealed record ValidationErrorData(IReadOnlyDictionary<string, string[]> Errors)
{
    public static ValidationErrorData FromModelState(ModelStateDictionary modelState) => new(
        modelState.Where(entry => entry.Value?.Errors.Count > 0).ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.Errors.Select(error => Normalize(error.ErrorMessage)).ToArray()));

    // Preserve explicit validation messages. Conversion exceptions have no public
    // message, so use a safe fallback without exposing parser internals.
    private static string Normalize(string message) =>
        string.IsNullOrWhiteSpace(message)
            ? "Invalid value. Please check the format and required fields."
            : message;
}

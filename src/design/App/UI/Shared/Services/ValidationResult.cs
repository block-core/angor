namespace App.UI.Shared.Services;

/// <summary>
/// Simple validation result that indicates whether validation passed or failed
/// and provides an error message if validation failed.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message if validation failed, or null if validation succeeded.
    /// </summary>
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, null);

    /// <summary>
    /// Creates a failed validation result with the specified error message.
    /// </summary>
    public static ValidationResult Fail(string errorMessage) => new(false, errorMessage);

    /// <summary>
    /// Combines multiple validation results and returns the first failure, or success if all passed.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        if (results == null || results.Length == 0)
            return Success();

        foreach (var result in results)
        {
            if (result != null && !result.IsValid)
                return result;
        }

        return Success();
    }

    public override string ToString()
    {
        return IsValid ? "Validation succeeded" : $"Validation failed: {ErrorMessage}";
    }
}

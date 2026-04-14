namespace App.UI.Shared.Services;

/// <summary>
/// Simple validator for project creation that handles all validation logic
/// with environment-specific rules (debug vs production).
/// </summary>
public class ProjectValidator
{
    private readonly bool _isDebugMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectValidator"/> class.
    /// </summary>
    /// <param name="isDebugMode">True if running in debug mode (less strict validation).</param>
    public ProjectValidator(bool isDebugMode)
    {
        _isDebugMode = isDebugMode;
    }

    /// <summary>
    /// Validates the project name.
    /// </summary>
    public ValidationResult ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Fail("Project name is required");

        if (name.Length > 200)
            return ValidationResult.Fail("Project name cannot exceed 200 characters");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates the project description.
    /// </summary>
    public ValidationResult ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ValidationResult.Fail("Project description is required");

        if (description.Length > 400)
            return ValidationResult.Fail("Description cannot exceed 400 characters");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates the target amount with environment-specific rules.
    /// </summary>
    public ValidationResult ValidateTargetAmount(decimal amount)
    {
        if (amount <= 0)
            return ValidationResult.Fail("Target amount must be greater than 0");

        // Debug mode: no min/max limits
        if (_isDebugMode)
            return ValidationResult.Success();

        // Production mode: enforce limits
        if (amount < 0.001m)
            return ValidationResult.Fail("Target amount must be at least 0.001 BTC");

        if (amount > 100m)
            return ValidationResult.Fail("Target amount cannot exceed 100 BTC");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates the penalty days with environment-specific rules.
    /// </summary>
    public ValidationResult ValidatePenaltyDays(int days)
    {
        if (days < 0)
            return ValidationResult.Fail("Penalty days cannot be negative");

        // Debug mode: no minimum
        if (_isDebugMode)
            return ValidationResult.Success();

        // Production mode: enforce minimum and maximum
        if (days < 10)
            return ValidationResult.Fail("Penalty period must be at least 10 days");

        if (days > 365)
            return ValidationResult.Fail("Penalty period cannot exceed 365 days");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates the funding end date with environment-specific rules.
    /// </summary>
    public ValidationResult ValidateFundingEndDate(DateTime endDate)
    {
        if (endDate.Date < DateTime.Now.Date)
            return ValidationResult.Fail("Funding end date must be on or after today");

        // Debug mode: allow same-day end date, no maximum period
        if (_isDebugMode)
            return ValidationResult.Success();

        // Production mode: must be in the future (not today) and within one year
        if (endDate.Date <= DateTime.Now.Date)
            return ValidationResult.Fail("Funding end date must be after today");

        var daysUntilEnd = (endDate - DateTime.Now).TotalDays;
        if (daysUntilEnd > 365)
            return ValidationResult.Fail("Funding period cannot exceed one year");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates stage release dates with environment-specific minimum days between stages.
    /// </summary>
    public ValidationResult ValidateStageReleaseDates(DateTime[] releaseDates, int minDaysBetween = 1)
    {
        if (releaseDates == null || releaseDates.Length == 0)
            return ValidationResult.Success();

        // Debug mode allows same-day stages
        if (_isDebugMode && minDaysBetween == 1)
            minDaysBetween = 0;

        for (int i = 1; i < releaseDates.Length; i++)
        {
            var daysBetween = (releaseDates[i] - releaseDates[i - 1]).TotalDays;
            if (daysBetween < minDaysBetween)
                return ValidationResult.Fail(
                    $"Stage {i + 1} must be at least {minDaysBetween} day(s) after previous stage");
        }

        return ValidationResult.Success();
    }
}

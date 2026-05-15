using Angor.Shared.Models;

namespace Angor.Shared.Protocol;

/// <summary>
/// Protocol-level validation for <see cref="ProjectInfo"/> parameters.
/// Enforces constraints that protect investors and ensure protocol safety.
/// This validator is called during project creation to reject invalid configurations.
/// </summary>
public static class ProjectInfoValidator
{
    /// <summary>
    /// Validates all protocol-level constraints on a ProjectInfo.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    /// <param name="projectInfo">The project info to validate.</param>
    /// <param name="isDebugMode">When true, relaxes constraints that would prevent testing
    /// (e.g. allows PenaltyDays=0 so penalty release flows can be exercised on signet).</param>
    public static string? Validate(ProjectInfo projectInfo, bool isDebugMode = false)
    {
        var error = ValidatePenaltyDays(projectInfo, isDebugMode);
        if (error != null) return error;

        error = ValidateTargetAmount(projectInfo);
        if (error != null) return error;

        error = ValidateStageTimelocks(projectInfo);
        if (error != null) return error;

        error = ValidateStageAmounts(projectInfo);
        if (error != null) return error;

        error = ValidateExpiryDate(projectInfo);
        if (error != null) return error;

        return null;
    }

    /// <summary>
    /// L2: Validates penalty duration is within protocol bounds.
    /// Penalty must be between <see cref="ProtocolConstants.MinPenaltyDays"/> and
    /// <see cref="ProtocolConstants.MaxPenaltyDays"/> for project types that enforce penalties.
    /// In debug mode, the minimum is relaxed to 0 to allow testing penalty release flows.
    /// </summary>
    public static string? ValidatePenaltyDays(ProjectInfo projectInfo, bool isDebugMode = false)
    {
        if (!projectInfo.HasPenalty)
            return null;

        // In debug mode, allow PenaltyDays=0 so tests can exercise penalty release
        // without waiting for real timelocks to mature.
        if (!isDebugMode && projectInfo.PenaltyDays < ProtocolConstants.MinPenaltyDays)
            return $"Penalty period must be at least {ProtocolConstants.MinPenaltyDays} days, got {projectInfo.PenaltyDays}.";

        if (projectInfo.PenaltyDays > ProtocolConstants.MaxPenaltyDays)
            return $"Penalty period cannot exceed {ProtocolConstants.MaxPenaltyDays} days, got {projectInfo.PenaltyDays}. " +
                   $"BIP-68 CSV encoding limits relative timelocks to ~388 days.";

        return null;
    }

    /// <summary>
    /// Validates the target amount meets the protocol minimum for project types that require it.
    /// </summary>
    public static string? ValidateTargetAmount(ProjectInfo projectInfo)
    {
        if (!projectInfo.RequiresTargetAmount)
            return null;

        if (projectInfo.TargetAmount < ProtocolConstants.MinTargetAmountSats)
            return $"Target amount must be at least {ProtocolConstants.MinTargetAmountSats} satoshis " +
                   $"(0.001 BTC), got {projectInfo.TargetAmount}.";

        return null;
    }

    /// <summary>
    /// H1: Validates that stage timelocks are sufficiently spaced and in the future.
    /// - For Invest projects: each stage release date must be at least
    ///   <see cref="ProtocolConstants.MinDaysBetweenStages"/> days after the previous one,
    ///   and the first stage must be at least <see cref="ProtocolConstants.MinDaysUntilFirstStage"/>
    ///   days after the project start date.
    /// - For Fund/Subscribe projects: validates DynamicStagePattern constraints.
    /// </summary>
    public static string? ValidateStageTimelocks(ProjectInfo projectInfo)
    {
        if (projectInfo.ProjectType == ProjectType.Invest)
        {
            return ValidateFixedStageTimelocks(projectInfo);
        }

        // Fund/Subscribe use DynamicStagePatterns — validate those
        return ValidateDynamicStagePatterns(projectInfo);
    }

    /// <summary>
    /// H5: Validates that stage percentage allocations won't produce dust outputs.
    /// For Invest projects, checks that each stage's percentage of the target amount
    /// (after the Angor fee) exceeds the dust threshold.
    /// </summary>
    public static string? ValidateStageAmounts(ProjectInfo projectInfo)
    {
        if (projectInfo.ProjectType != ProjectType.Invest)
            return null;

        if (projectInfo.Stages == null || projectInfo.Stages.Count == 0)
            return null;

        // Check that no stage percentage is zero or negative
        for (int i = 0; i < projectInfo.Stages.Count; i++)
        {
            if (projectInfo.Stages[i].AmountToRelease <= 0)
                return $"Stage {i + 1} has invalid release percentage: {projectInfo.Stages[i].AmountToRelease}%. " +
                       $"Each stage must release a positive percentage.";
        }

        // Check that the minimum possible investment won't produce dust outputs.
        // With a target amount and N stages, the smallest stage gets:
        //   minStageAmount = targetAmount * (1 - feePercent/100) * (minStagePercent/100)
        // We validate against the dust threshold. We use 3% as max expected fee.
        decimal minStagePercent = projectInfo.Stages.Min(s => s.AmountToRelease);
        long estimatedMinStageAmount = (long)(projectInfo.TargetAmount * 0.97m * (minStagePercent / 100m));

        if (estimatedMinStageAmount < ProtocolConstants.DustThresholdSats)
            return $"Stage with {minStagePercent}% allocation would produce an output of ~{estimatedMinStageAmount} sats " +
                   $"at the target amount, which is below the dust threshold of {ProtocolConstants.DustThresholdSats} sats. " +
                   $"Increase the target amount or adjust stage percentages.";

        return null;
    }

    /// <summary>
    /// Validates that the expiry date is after the last stage release date.
    /// </summary>
    public static string? ValidateExpiryDate(ProjectInfo projectInfo)
    {
        if (projectInfo.ExpiryDate == default)
            return "Expiry date must be set.";

        if (projectInfo.ProjectType == ProjectType.Invest && projectInfo.Stages != null && projectInfo.Stages.Count > 0)
        {
            var lastStageDate = projectInfo.Stages.Max(s => s.ReleaseDate);
            if (projectInfo.ExpiryDate <= lastStageDate)
                return $"Expiry date ({projectInfo.ExpiryDate:yyyy-MM-dd}) must be after the last stage release date " +
                       $"({lastStageDate:yyyy-MM-dd}).";
        }

        return null;
    }

    private static string? ValidateFixedStageTimelocks(ProjectInfo projectInfo)
    {
        if (projectInfo.Stages == null || projectInfo.Stages.Count == 0)
            return "Invest projects must have at least one stage.";

        var orderedStages = projectInfo.Stages.OrderBy(s => s.ReleaseDate).ToList();

        // First stage must be sufficiently after the start date
        if (projectInfo.StartDate != DateTime.MinValue)
        {
            var daysUntilFirstStage = (orderedStages[0].ReleaseDate - projectInfo.StartDate).TotalDays;
            if (daysUntilFirstStage < ProtocolConstants.MinDaysUntilFirstStage)
                return $"First stage release date must be at least {ProtocolConstants.MinDaysUntilFirstStage} day(s) " +
                       $"after the project start date. Got {daysUntilFirstStage:F1} days.";
        }

        // Each subsequent stage must be sufficiently after the previous
        for (int i = 1; i < orderedStages.Count; i++)
        {
            var daysBetween = (orderedStages[i].ReleaseDate - orderedStages[i - 1].ReleaseDate).TotalDays;
            if (daysBetween < ProtocolConstants.MinDaysBetweenStages)
                return $"Stage {i + 1} release date must be at least {ProtocolConstants.MinDaysBetweenStages} day(s) " +
                       $"after stage {i}. Got {daysBetween:F1} days between " +
                       $"{orderedStages[i - 1].ReleaseDate:yyyy-MM-dd} and {orderedStages[i].ReleaseDate:yyyy-MM-dd}.";
        }

        return null;
    }

    private static string? ValidateDynamicStagePatterns(ProjectInfo projectInfo)
    {
        if (projectInfo.DynamicStagePatterns == null || projectInfo.DynamicStagePatterns.Count == 0)
        {
            if (projectInfo.AllowDynamicStages)
                return "Fund/Subscribe projects must have at least one DynamicStagePattern.";
            return null;
        }

        foreach (var pattern in projectInfo.DynamicStagePatterns)
        {
            if (pattern.StageCount <= 0)
                return $"DynamicStagePattern must have at least 1 stage, got {pattern.StageCount}.";

            if (pattern.Amount.HasValue && pattern.Amount.Value <= 0)
                return $"DynamicStagePattern amount must be positive, got {pattern.Amount.Value}.";
        }

        return null;
    }
}

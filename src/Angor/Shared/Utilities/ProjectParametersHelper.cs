using Angor.Shared.Models;

namespace Angor.Shared.Utilities;

public static class ProjectParametersHelper
{
    public static int GetStageCount(ProjectInfo projectInfo, FundingParameters parameters)
    {
        if (projectInfo == null)
            throw new ArgumentNullException(nameof(projectInfo));

        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        if (projectInfo.ProjectType == ProjectType.Invest)
        {
            return projectInfo.Stages.Count;
        }

        if (parameters.StageCountOverride.HasValue && parameters.StageCountOverride.Value > 0)
        {
            return parameters.StageCountOverride.Value;
        }

        if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
        {
            throw new InvalidOperationException("Fund/Subscribe projects must have at least one DynamicStagePattern");
        }

        if (parameters.PatternIndex >= projectInfo.DynamicStagePatterns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.PatternIndex),
             $"Pattern index {parameters.PatternIndex} is out of range. Project has {projectInfo.DynamicStagePatterns.Count} patterns.");
        }

        var pattern = projectInfo.DynamicStagePatterns[parameters.PatternIndex];
        return pattern.StageCount;
    }

    public static DateTime? GetExpiryDateOverride(ProjectInfo projectInfo, long investmentAmount)
    {
        return PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, investmentAmount);
    }
}

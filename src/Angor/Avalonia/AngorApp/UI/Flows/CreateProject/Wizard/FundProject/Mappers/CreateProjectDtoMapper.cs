using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Models;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Mappers;

public static class CreateProjectDtoMapper
{
    public static CreateProjectDto ToDto(this IFundProjectConfig fundProject)
    {
        var satsValue = fundProject.GoalAmount?.Sats ?? 0;

        return new CreateProjectDto
        {

            ProjectName = fundProject.Name,
            Description = fundProject.Description,
            WebsiteUri = fundProject.Website,
            AvatarUri = fundProject.AvatarUri,
            BannerUri = fundProject.BannerUri,
            Nip05 = fundProject.Nip05,
            Lud16 = fundProject.Lud16,
            Nip57 = fundProject.Nip57,


            ProjectType = Angor.Shared.Models.ProjectType.Fund,
            Sats = satsValue,
            StartDate = DateTime.Now,

            EndDate = null,

            TargetAmount = new Amount(satsValue),
            PenaltyDays = 0,
            PenaltyThreshold = fundProject.Threshold?.Sats,

            Stages = Enumerable.Empty<CreateProjectStageDto>(),
            SelectedPatterns = CreateDynamicStagePatterns(
                fundProject.PayoutFrequency,
                fundProject.SelectedInstallments.SelectedItems
            ),
            PayoutDay = fundProject.MonthlyPayoutDate
        };
    }

    private static List<DynamicStagePattern>? CreateDynamicStagePatterns(
        PayoutFrequency? frequency,
        IEnumerable<int> installmentCounts)
    {
        if (!frequency.HasValue || installmentCounts == null || !installmentCounts.Any())
        {
            return null;
        }

        var stageFrequency = frequency.Value == PayoutFrequency.Monthly
            ? StageFrequency.Monthly
            : StageFrequency.Weekly;

        var standardPatterns = DynamicStagePattern.GetStandardPatterns();

        var results = installmentCounts.Where(count => count > 0).Select(count =>
        {
            var matchingStandard = standardPatterns.FirstOrDefault(p => p.Frequency == stageFrequency && p.StageCount == count);

            if (matchingStandard == null)
            {
                throw new InvalidOperationException(
                    $"No standard pattern found for frequency '{stageFrequency}' with {count} stages. " +
                    $"Available patterns: {string.Join(", ", standardPatterns.Select(p => $"{p.Frequency}/{p.StageCount}"))}");
            }

            return matchingStandard;
        }).ToList();

        return results;
    }
}

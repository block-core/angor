using System.Linq;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Models;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Mappers;

public static class CreateProjectDtoMapper
{
    public static CreateProjectDto ToDto(this IInvestmentProjectConfig newProject)
    {
        var satsValue = newProject.TargetAmount?.Sats ?? 0;
        var penaltyDaysValue = newProject.PenaltyDays ?? 0;

        return new CreateProjectDto
        {

            ProjectName = newProject.Name,
            Description = newProject.Description,
            WebsiteUri = newProject.Website,
            AvatarUri = newProject.AvatarUri,
            BannerUri = newProject.BannerUri,
            Nip05 = newProject.Nip05,
            Lud16 = newProject.Lud16,
            Nip57 = newProject.Nip57,


            ProjectType = newProject.ProjectType,
            Sats = satsValue,
            StartDate = newProject.StartDate ?? DateTime.Now,

            ExpiryDate = newProject.ExpiryDate,
            EndDate = newProject.FundingEndDate,
            PenaltyDays = penaltyDaysValue,
            PenaltyThreshold = newProject.PenaltyThreshold,
            TargetAmount = new Amount(satsValue),
            Stages = newProject.Stages.Select(stage =>
                    new CreateProjectStageDto(DateOnly.FromDateTime(stage.ReleaseDate!.Value.Date), stage.Percent ?? 0)),



            SelectedPatterns = null,
            PayoutDay = null
        };
    }
}

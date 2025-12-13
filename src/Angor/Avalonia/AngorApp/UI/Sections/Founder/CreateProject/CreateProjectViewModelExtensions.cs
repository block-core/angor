using System.Linq;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Models;

namespace AngorApp.UI.Sections.Founder.CreateProject;

public static class CreateProjectViewModelExtensions
{
    public static CreateProjectDto ToDto(this ICreateProjectViewModel createProjectViewModel)
    {
        var fundingVm = createProjectViewModel.FundingStructureViewModel;
        var projectType = fundingVm.ProjectType;

        // For Invest projects, Sats and PenaltyDays are required
        // For Fund/Subscribe projects, they may be optional or 0
        var satsValue = projectType == ProjectType.Invest
       ? fundingVm.Sats!.Value
      : fundingVm.Sats ?? 0;

        var penaltyDaysValue = fundingVm.PenaltyDays ?? 0;

        return new CreateProjectDto
        {
            //Nostr profile
            ProjectName = createProjectViewModel.ProfileViewModel.ProjectName!,
            Description = createProjectViewModel.ProfileViewModel.Description!,
            AvatarUri = createProjectViewModel.ProfileViewModel.AvatarUri!,
            BannerUri = createProjectViewModel.ProfileViewModel.BannerUri!,
            WebsiteUri = createProjectViewModel.ProfileViewModel.WebsiteUri,
            Nip57 = null, //TODO: Map result...
            Nip05 = null, //TODO: Map result...
            Lud16 = null, //TODO: Map result...

            //Project information
            ProjectType = projectType,
            Sats = satsValue,
            StartDate = fundingVm.FundingStartDate,
            ExpiryDate = fundingVm.ExpiryDate,
            EndDate = fundingVm.FundingEndDate,
            PenaltyDays = penaltyDaysValue,
            PenaltyThreshold = fundingVm.PenaltyThreshold,
            TargetAmount = new Amount(satsValue),
            Stages = createProjectViewModel.StagesViewModel.Stages.Select(stage => 
                    new CreateProjectStageDto(DateOnly.FromDateTime(stage.ReleaseDate!.Value.Date), stage.Percent!.Value)),

            // For Fund and Subscribe types - convert ObservableCollection to List
            SelectedPatterns = fundingVm.SelectedPatterns?.ToList(),
            PayoutDay = fundingVm.PayoutDay
        };
    }
}
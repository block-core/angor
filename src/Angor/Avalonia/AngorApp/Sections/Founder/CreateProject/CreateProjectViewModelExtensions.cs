using System.Linq;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;

namespace AngorApp.Sections.Founder.CreateProject;

public static class CreateProjectViewModelExtensions
{
    public static CreateProjectDto ToDto(this ICreateProjectViewModel createProjectViewModel)
    {
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
            Sats = createProjectViewModel.FundingStructureViewModel.Sats!.Value,
            StartDate = createProjectViewModel.FundingStructureViewModel.StartDate,
            ExpiryDate = createProjectViewModel.FundingStructureViewModel.ExpiryDate,
            EndDate = createProjectViewModel.FundingStructureViewModel.EndDate,
            PenaltyDays = createProjectViewModel.FundingStructureViewModel.PenaltyDays!.Value,
            TargetAmount = new Amount(createProjectViewModel.FundingStructureViewModel.Sats!.Value),
            Stages = createProjectViewModel.StagesViewModel.Stages.Select(stage => new CreateProjectStageDto(DateOnly.FromDateTime(stage.ReleaseDate!.Value.Date), stage.Percent!.Value))
        };
    }
}
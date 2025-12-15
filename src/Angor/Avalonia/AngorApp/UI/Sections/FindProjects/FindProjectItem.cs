using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.Browse.Details;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectItem(
        ProjectDto dto,
        ProjectStatisticsDto statistics,
        IProjectAppService projectAppService,
        Func<IFullProject, IProjectDetailsViewModel> projectDetailsViewModelFactory,
        INavigator navigator
    ) : IFindProjectItem
    {
        public string Name => dto.Name;
        public IAmountUI FundingTarget => new AmountUI(dto.TargetAmount);

        public IAmountUI FundingRaised => new AmountUI(statistics.TotalInvested);
        public string Description => dto.ShortDescription;

        public int InvestorsCount => statistics.TotalInvestors ?? 0;

        public Uri BannerUrl => dto.Banner!;

        public Uri LogoUrl => dto.Avatar!;
        public ProjectId Id => dto.Id;
        public IEnhancedCommand GoToDetails => EnhancedCommand.Create(() =>
        {
            return projectAppService.GetFullProject(Id)
                                    .Map(project => projectDetailsViewModelFactory(project))
                                    .Map(details => navigator.Go(() => details));
            
        });
    }
}
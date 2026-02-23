using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.FindProjects.Details;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.FindProjects
{
    public partial class FindProjectItem : ReactiveObject, IFindProjectItem, IDisposable
    {
        private readonly ProjectDto dto;
        private readonly IProjectAppService projectAppService;
        private readonly CompositeDisposable disposable = new();

        [ObservableAsProperty] private IAmountUI? fundingRaised;
        [ObservableAsProperty] private int? investorsCount;

        public FindProjectItem(
            ProjectDto dto,
            IProjectAppService projectAppService,
            Func<IFullProject, IDetailsViewModel> detailsFactory,
            INavigator navigator)
        {
            this.dto = dto;
            this.projectAppService = projectAppService;

            var loadStatistics = EnhancedCommand.Create(DoLoadStatistics).DisposeWith(disposable);
            LoadStatistics = loadStatistics;

            var statistics = loadStatistics
                             .Successes()
                             .Publish();

            GoToDetails = EnhancedCommand.Create(() => projectAppService.GetFullProject(Id).Map(project => navigator.Go(() => detailsFactory(project))));
            
            fundingRaisedHelper = statistics.Select(statisticsDto => new AmountUI(statisticsDto.TotalInvested)).ToProperty(this, item => item.FundingRaised);
            investorsCountHelper = statistics.Select(statisticsDto => statisticsDto.TotalInvestors).ToProperty(this, item => item.InvestorsCount);
            
            statistics.Connect().DisposeWith(disposable);
        }

        private Task<Result<ProjectStatisticsDto>> DoLoadStatistics()
        {
            return projectAppService.GetProjectStatistics(dto.Id);
        }

        public string Name => dto.Name;
        public IAmountUI FundingTarget => new AmountUI(dto.TargetAmount);
        public Uri BannerUrl => dto.Banner!;
        public Uri LogoUrl => dto.Avatar!;
        public ProjectId Id => dto.Id;
        public string Description => dto.ShortDescription;
        public IEnhancedCommand LoadStatistics { get; }
        public IEnhancedCommand GoToDetails { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
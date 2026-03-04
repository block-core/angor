using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public class InvestmentProject : Project, IInvestmentProject
    {
        public InvestmentProject(ProjectDto seed, IProjectAppService projectAppService) : base(seed)
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            Raised = refresh.Successes().Select(dto => new AmountUI(dto.TotalInvested));
            InvestorCount = refresh.Successes().Select(dto => dto.TotalInvestors ?? 0);
            Target = new AmountUI(seed.TargetAmount);
            Refresh = refresh;
            var seedStages = seed.Stages ?? [];
            var initialStages = Stage.MapFrom(seedStages, seed.TargetAmount);
            Stages = refresh.Successes()
                .Select(_ => Stage.MapFrom(seedStages, seed.TargetAmount))
                .StartWith(initialStages);
            PenaltyDuration = seed.PenaltyDuration;
            PenaltyThreshold = seed.PenaltyThreshold is { } threshold ? new AmountUI(threshold) : null;
            ProjectStatus = refresh.Successes().Select(dto =>
            {
                var fundingFinished = DateTime.UtcNow.Date >= seed.FundingEndDate.Date;
                var reachedTarget = dto.TotalInvested >= seed.TargetAmount;
                return fundingFinished && !reachedTarget ? ProjectsV2.ProjectStatus.Closed : ProjectsV2.ProjectStatus.Open;
            }).StartWith(DateTime.UtcNow.Date >= seed.FundingEndDate.Date ? ProjectsV2.ProjectStatus.Closed : ProjectsV2.ProjectStatus.Open);
        }

        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<int> InvestorCount { get; }
        public override IObservable<int> SupporterCount => InvestorCount;
        public override IAmountUI FundingTarget => Target;
        public override IObservable<IAmountUI> FundingRaised => Raised;
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; }
        public TimeSpan PenaltyDuration { get; }
        public IAmountUI? PenaltyThreshold { get; }
        public override IEnhancedCommand Refresh { get; }
        public override IObservable<ProjectStatus> ProjectStatus { get; }
    }
}

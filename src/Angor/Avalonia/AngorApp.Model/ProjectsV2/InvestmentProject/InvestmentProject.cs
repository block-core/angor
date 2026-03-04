using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public class InvestmentProject : Project, IInvestmentProject
    {
        public InvestmentProject(ProjectDto seed, IProjectAppService projectAppService, IEnhancedCommand<Result> invest) : base(seed, invest)
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            var projectStatistics = refresh.Successes();
            var seedStages = seed.Stages ?? [];
            IReadOnlyCollection<IStage> mapStages() => Stage.MapFrom(seedStages, seed.TargetAmount);

            Raised = projectStatistics.Select(ToRaisedAmount);
            InvestorCount = projectStatistics.Select(ToInvestorCount);
            Target = new AmountUI(seed.TargetAmount);
            FundingStart = seed.FundingStartDate;
            FundingEnd = seed.FundingEndDate;
            Refresh = refresh;
            Stages = projectStatistics
                .Select(_ => mapStages())
                .StartWith(mapStages());
            PenaltyDuration = seed.PenaltyDuration;
            PenaltyThreshold = ToPenaltyThreshold(seed);
        }

        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<int> InvestorCount { get; }
        public DateTimeOffset FundingStart { get; }
        public DateTimeOffset FundingEnd { get; }
        public override IObservable<int> SupporterCount => InvestorCount;
        public override IAmountUI FundingTarget => Target;
        public override IObservable<IAmountUI> FundingRaised => Raised;
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; }
        public TimeSpan PenaltyDuration { get; }
        public IAmountUI? PenaltyThreshold { get; }
        public override IEnhancedCommand Refresh { get; }

        private static IAmountUI ToRaisedAmount(ProjectStatisticsDto statistics)
        {
            return new AmountUI(statistics.TotalInvested);
        }

        private static int ToInvestorCount(ProjectStatisticsDto statistics)
        {
            return statistics.TotalInvestors ?? 0;
        }

        private static IAmountUI? ToPenaltyThreshold(ProjectDto seed)
        {
            return seed.PenaltyThreshold is { } threshold ? new AmountUI(threshold) : null;
        }

        private static bool IsFundingFinished(DateTimeOffset fundingEndDate)
        {
            return DateTime.UtcNow.Date >= fundingEndDate.Date;
        }
    }
}

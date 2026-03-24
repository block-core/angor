using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public class InvestmentProject : Project, IInvestmentProject
    {
        public InvestmentProject(ProjectDto seed, IProjectAppService projectAppService, IEnhancedCommand<Result> invest, IEnhancedCommand? manageFunds = null) : base(seed, invest, manageFunds ?? CreateUnsupportedManageFundsCommand())
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            var projectStatistics = refresh.Successes();
            var seedStages = seed.Stages ?? [];
            IReadOnlyCollection<IStage> mapStages() => Stage.MapFrom(seedStages, seed.TargetAmount);

            Raised = projectStatistics.Select(ToRaisedAmount).ReplayLastActive();
            TotalInvestment = projectStatistics.Select(ToTotalInvestment).ReplayLastActive();
            AvailableBalance = projectStatistics.Select(ToAvailableBalance).ReplayLastActive();
            Withdrawable = projectStatistics.Select(ToWithdrawable).ReplayLastActive();
            TotalStages = projectStatistics.Select(ToTotalStages).ReplayLastActive();
            InvestorCount = projectStatistics.Select(ToInvestorCount).ReplayLastActive();
            Target = new AmountUI(seed.TargetAmount);
            FundingStart = seed.FundingStartDate;
            FundingEnd = seed.FundingEndDate;
            FundingState = Observable.Return(IsFundingOpenNow(FundingStart, FundingEnd))
                .CombineLatest(Raised, (isOpen, raised) => ResolveFundingState(isOpen, raised, Target))
                .DistinctUntilChanged()
                .ReplayLastActive();
            Refresh = refresh;
            Stages = projectStatistics
                .Select(_ => mapStages())
                .StartWith(mapStages())
                .ReplayLastActive();
            PenaltyDuration = seed.PenaltyDuration;
            PenaltyThreshold = ToPenaltyThreshold(seed);
        }

        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<InvestmentFundingState> FundingState { get; }
        public IObservable<IAmountUI> TotalInvestment { get; }
        public IObservable<IAmountUI> AvailableBalance { get; }
        public IObservable<IAmountUI> Withdrawable { get; }
        public IObservable<int> TotalStages { get; }
        public IObservable<int> InvestorCount { get; }
        public DateTime FundingStart { get; }
        public DateTime FundingEnd { get; }
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

        private static IAmountUI ToTotalInvestment(ProjectStatisticsDto statistics)
        {
            return new AmountUI(statistics.TotalInvested);
        }

        private static IAmountUI ToAvailableBalance(ProjectStatisticsDto statistics)
        {
            return new AmountUI(statistics.AvailableBalance);
        }

        private static IAmountUI ToWithdrawable(ProjectStatisticsDto statistics)
        {
            return new AmountUI(statistics.WithdrawableAmount);
        }

        private static int ToTotalStages(ProjectStatisticsDto statistics)
        {
            return statistics.TotalStages;
        }

        private static int ToInvestorCount(ProjectStatisticsDto statistics)
        {
            return statistics.TotalInvestors ?? 0;
        }

        private static IAmountUI? ToPenaltyThreshold(ProjectDto seed)
        {
            return seed.PenaltyThreshold is { } threshold ? new AmountUI(threshold) : null;
        }

        private static bool IsFundingOpenNow(DateTime fundingStartDate, DateTime fundingEndDate)
        {
            var now = DateTime.UtcNow.Date;
            return now >= fundingStartDate.Date && now <= fundingEndDate.Date;
        }

        internal static InvestmentFundingState ResolveFundingState(bool isOpen, IAmountUI raised, IAmountUI target)
        {
            if (isOpen)
            {
                return InvestmentFundingState.Open;
            }

            return raised.Sats >= target.Sats
                ? InvestmentFundingState.Successful
                : InvestmentFundingState.Failed;
        }
    }
}

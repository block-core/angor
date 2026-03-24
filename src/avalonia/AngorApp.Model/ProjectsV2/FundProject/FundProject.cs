using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Model.ProjectsV2.FundProject
{
    public class FundProject : Project, IFundProject
    {
        public FundProject(ProjectDto seed, IProjectAppService projectAppService, IEnhancedCommand<Result> invest, IEnhancedCommand? manageFunds = null) : base(seed, invest, manageFunds ?? CreateUnsupportedManageFundsCommand())
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            var projectStatistics = refresh.Successes();

            Funded = projectStatistics.Select(ToFundedAmount).ReplayLastActive();
            FunderCount = projectStatistics.Select(ToFunderCount).ReplayLastActive();
            Goal = new AmountUI(seed.TargetAmount);
            Refresh = refresh;
            Payments = projectStatistics
                .Select(ToPayments)
                .StartWith([])
                .ReplayLastActive();
        }

        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; }
        public IObservable<int> FunderCount { get; }
        public override IObservable<int> SupporterCount => FunderCount;
        public override IAmountUI FundingTarget => Goal;
        public override IObservable<IAmountUI> FundingRaised => Funded;
        public DateTimeOffset TransactionDate { get; set; }
        public override IEnhancedCommand Refresh { get; }

        private static IAmountUI ToFundedAmount(ProjectStatisticsDto statistics)
        {
            return new AmountUI(statistics.TotalInvested);
        }

        private static int ToFunderCount(ProjectStatisticsDto statistics)
        {
            return statistics.TotalInvestors ?? 0;
        }

        private static IReadOnlyCollection<IPayment> ToPayments(ProjectStatisticsDto statistics)
        {
            return Payment.MapFrom(statistics.DynamicStages ?? []);
        }
    }
}

using Zafiro.Reactive;

namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public interface IInvestmentProject : IProject
    {
        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<InvestmentFundingState> FundingState { get; }
        public IObservable<IAmountUI> TotalInvestment { get; }
        public IObservable<IAmountUI> AvailableBalance { get; }
        public IObservable<IAmountUI> Withdrawable { get; }
        public IObservable<int> TotalStages { get; }
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; }
        public IObservable<int> InvestorCount { get; }
        public DateTime FundingStart { get; }
        public DateTime FundingEnd { get; }
        public TimeSpan PenaltyDuration { get; }
        public IAmountUI? PenaltyThreshold { get; }
        IObservable<bool> IsNotInvestedYet => Observable.Return(true);
        IObservable<bool> IsFundingOpen => FundingState.Select(state => state == InvestmentFundingState.Open);
        IObservable<bool> IsFundingSuccessful => FundingState.Select(state => state == InvestmentFundingState.Successful);
        IObservable<bool> IsFundingFailed => FundingState.Select(state => state == InvestmentFundingState.Failed);
        IObservable<decimal> InvestmentProgress => Raised.Select(x => (decimal)x.Sats / Target.Sats);
    }
}

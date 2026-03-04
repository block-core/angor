using Zafiro.Reactive;

namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public interface IInvestmentProject : IProject
    {
        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<IAmountUI> TotalInvestment { get; }
        public IObservable<IAmountUI> AvailableBalance { get; }
        public IObservable<IAmountUI> Withdrawable { get; }
        public IObservable<int> TotalStages { get; }
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; }
        public IObservable<int> InvestorCount { get; }
        public DateTimeOffset FundingStart { get; }
        public DateTimeOffset FundingEnd { get; }
        public TimeSpan PenaltyDuration { get; }
        public IAmountUI? PenaltyThreshold { get; }
        IObservable<bool> IsNotInvestedYet => Observable.Return(true);
        IObservable<bool> IsFundingOpen
        {
            get
            {
                DateTimeOffset now = DateTimeOffset.Now;
                var isOpen = now >= FundingStart && now <= FundingEnd;
                return Observable.Return(isOpen);
            }
        }
        IObservable<bool> IsFundingSuccessful => IsFundingOpen.CombineLatest(Raised, (isOpen, raised) => !isOpen && raised.Sats >= Target.Sats);
        IObservable<bool> IsFundingFailed => IsFundingOpen.CombineLatest(Raised, (isOpen, raised) => !isOpen && raised.Sats < Target.Sats);
        IObservable<decimal> InvestmentProgress => Raised.Select(x => (decimal)x.Sats / Target.Sats);
    }
}

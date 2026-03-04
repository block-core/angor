namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public interface IInvestmentProject : IProject
    {
        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; }
        public IObservable<int> InvestorCount { get; }
        public TimeSpan PenaltyDuration { get; }
        public IAmountUI? PenaltyThreshold { get; }
    }
}

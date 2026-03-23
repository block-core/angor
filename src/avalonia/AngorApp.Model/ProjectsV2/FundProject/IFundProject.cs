namespace AngorApp.Model.ProjectsV2.FundProject
{
    public interface IFundProject : IProject
    {
        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; }
        public DateTimeOffset TransactionDate { get; }
        public IObservable<int> FunderCount { get; }
        IObservable<bool> IsGoalReached => Funded.Select(funded => funded.Sats >= Goal.Sats);
        IObservable<decimal> FundProgress => Funded.Select(x => (decimal)x.Sats / Goal.Sats);
    }
}

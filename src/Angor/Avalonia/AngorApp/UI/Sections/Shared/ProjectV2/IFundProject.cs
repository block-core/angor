namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public interface IFundProject : IProject
    {
        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; }
        public DateTimeOffset FundingStart { get; }
        public DateTimeOffset FundingEnd { get; }
        public DateTimeOffset TransactionDate { get; }
        public FundingStatus Status { get; }
    }
}
using Angor.Shared.Models;

namespace AngorApp.Model.ProjectsV2.FundProject
{
    public interface IFundProject : IProject
    {
        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; }
        public DateTimeOffset TransactionDate { get; }
        public FundingStatus Status { get; }
        public IObservable<int> FunderCount { get; }
        public IReadOnlyList<DynamicStagePattern> DynamicStagePatterns { get; }
    }
}
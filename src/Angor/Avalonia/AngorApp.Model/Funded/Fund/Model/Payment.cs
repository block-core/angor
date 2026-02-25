using AngorApp.Model.ProjectsV2.FundProject;

namespace AngorApp.Model.Funded.Fund.Model
{
    public class Payment : IPayment
    {
        public int Id { get; } = 1;
        public DateTimeOffset PaymentDate { get; } = DateTimeOffset.Now;
        public PaymentStatus Status { get; } = PaymentStatus.Pending;
        public IAmountUI Amount { get; } = AmountUI.FromBtc(1m);
        public decimal Ratio => 0.4m;
    }
}
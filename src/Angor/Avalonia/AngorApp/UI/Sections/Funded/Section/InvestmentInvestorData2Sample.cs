namespace AngorApp.UI.Sections.Funded.Section
{
    internal class InvestmentInvestorData2Sample : IInvestmentInvestorData2
    {
        public IAmountUI Amount { get; } = new AmountUI(10000000);
        public DateTimeOffset InvestedOn { get; } = DateTimeOffset.Now;
    }
}
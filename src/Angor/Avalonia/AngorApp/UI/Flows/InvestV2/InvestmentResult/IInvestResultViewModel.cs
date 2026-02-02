using AngorApp.Model.Contracts.Amounts;

namespace AngorApp.UI.Flows.InvestV2.InvestmentResult
{
    public interface IInvestResultViewModel
    {
        IAmountUI Amount { get; set; }
    }
}
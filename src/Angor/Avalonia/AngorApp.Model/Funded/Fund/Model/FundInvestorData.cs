using Angor.Sdk.Funding.Investor;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.Shared.Services;

namespace AngorApp.Model.Funded.Fund.Model
{
    public sealed class FundInvestorData : InvestorDataBase, IFundInvestorData
    {
        public FundInvestorData(InvestedProjectDto dto, IInvestmentAppService investmentAppService, IWalletContext walletContext)
            : base(dto, investmentAppService, walletContext)
        {
        }
    }
}

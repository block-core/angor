using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Manage;
using AngorApp.UI.Sections.FundedV2.Investment.Model;

namespace AngorApp.UI.Sections.FundedV2.Host.Manage
{
    public class ManageViewModelSample : IManageViewModel
    {
        public ManageViewModelSample() : this(new InvestmentFunded(new InvestmentProjectSample(), new InvestmentInvestorDataSample()))
        {
        }

        public ManageViewModelSample(IFunded funded)
        {
            Funded = funded;
            CancelApproval = EnhancedCommand.CreateWithResult(Result.Success);
            OpenChat = EnhancedCommand.CreateWithResult(Result.Success);
            CancelInvestment = EnhancedCommand.CreateWithResult(Result.Success);
            ConfirmInvestment = EnhancedCommand.CreateWithResult(Result.Success);
        }

        public IFunded Funded { get; }
        public IEnhancedCommand<Result> CancelApproval { get; }
        public IEnhancedCommand<Result> OpenChat { get; }
        public IEnhancedCommand<Result> CancelInvestment { get; }
        public IEnhancedCommand<Result> ConfirmInvestment { get; }
    }
}

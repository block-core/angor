using Angor.Sdk.Funding.Investor;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.Shared.Services;

namespace AngorApp.Model.Funded.Fund.Model
{
    public class FundFunded : FundedBase, IFundFunded
    {
        public FundFunded(
            IFundProject project,
            IFundInvestorData investorData,
            INotificationService notificationService,
            ITransactionDraftPreviewer draftPreviewer,
            IInvestmentAppService appService,
            IWalletContext walletContext
        ) : base(project, investorData, notificationService, draftPreviewer, appService, walletContext)
        {
        }

        public new IFundProject Project => (IFundProject)base.Project;
        public new IFundInvestorData InvestorData => (IFundInvestorData)base.InvestorData;

        IProject IFunded.Project => base.Project;
        IInvestorData IFunded.InvestorData => base.InvestorData;
    }
}

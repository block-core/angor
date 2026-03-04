using Angor.Sdk.Funding.Investor;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.Model.Shared.Services;

namespace AngorApp.Model.Funded.Investment.Model
{
    public class InvestmentFunded : FundedBase, IInvestmentFunded
    {
        public InvestmentFunded(
            IInvestmentProject project,
            IInvestmentInvestorData investorData,
            INotificationService notificationService,
            ITransactionDraftPreviewer draftPreviewer,
            IInvestmentAppService appService,
            IWalletContext walletContext
        ) : base(project, investorData, notificationService, draftPreviewer, appService, walletContext)
        {
        }

        public new IInvestmentProject Project => (IInvestmentProject)base.Project;
        public new IInvestmentInvestorData InvestorData => (IInvestmentInvestorData)base.InvestorData;

        IProject IFunded.Project => base.Project;
        IInvestorData IFunded.InvestorData => base.InvestorData;
    }
}

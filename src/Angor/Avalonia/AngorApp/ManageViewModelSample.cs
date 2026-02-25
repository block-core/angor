using System.Reactive.Subjects;
using Angor.Sdk.Funding.Founder;
using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.Funded.ProjectList.Item;

namespace AngorApp
{
    public class ManageViewModelSample : IManageViewModel
    {
        private readonly BehaviorSubject<InvestmentStatus> statusSubject = new(InvestmentStatus.FounderSignaturesReceived);
        private readonly FundedProjectItemSample project;
        private InvestmentStatus status = InvestmentStatus.FounderSignaturesReceived;

        public ManageViewModelSample()
        {
            project = new FundedProjectItemSample();

            if (project.Investment is InvestmentSample investment)
            {
                investment.Status = statusSubject;
            }
        }

        public InvestmentStatus Status
        {
            get => status;
            set
            {
                status = value;
                statusSubject.OnNext(value);
            }
        }

        public IFundedProject Project => project;
        public IEnhancedCommand<Result> CancelApproval { get; }
        public IEnhancedCommand<Result> OpenChat { get; }
        public IEnhancedCommand<Result> CancelInvestment { get; }
        public IEnhancedCommand<Result> ConfirmInvestment { get; }
    }
}

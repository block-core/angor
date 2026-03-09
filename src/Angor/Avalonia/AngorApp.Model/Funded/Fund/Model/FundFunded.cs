using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Dtos;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.Shared.Services;
using Zafiro.Reactive;

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
            PaymentsWithStatus = project.Payments
                .CombineLatest(investorData.StageItems, ApplyStatuses)
                .ReplayLastActive();
        }

        public new IFundProject Project => (IFundProject)base.Project;
        public new IFundInvestorData InvestorData => (IFundInvestorData)base.InvestorData;
        public IObservable<IReadOnlyCollection<IPayment>> PaymentsWithStatus { get; }

        IProject IFunded.Project => base.Project;
        IInvestorData IFunded.InvestorData => base.InvestorData;

        private static IReadOnlyCollection<IPayment> ApplyStatuses(
            IReadOnlyCollection<IPayment> payments,
            IReadOnlyList<InvestorStageItemDto> recoveryItems)
        {
            if (recoveryItems.Count == 0)
                return payments;

            var itemsByIndex = recoveryItems.ToDictionary(i => i.StageIndex);

            return payments.Select(payment =>
            {
                if (itemsByIndex.TryGetValue(payment.Id, out var item) && !string.IsNullOrEmpty(item.Status))
                {
                    return payment is ProjectsV2.FundProject.Payment p ? p.WithStatus(item.Status) : payment;
                }
                return payment;
            }).ToList();
        }
    }
}

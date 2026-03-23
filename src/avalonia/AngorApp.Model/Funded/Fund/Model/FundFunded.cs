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

            var datesByIndex = payments.ToDictionary(p => p.Id, p => p.PaymentDate);

            return recoveryItems
                .OrderBy(item => item.StageIndex)
                .Select(item => (IPayment)new ProjectsV2.FundProject.Payment(
                    id: item.StageIndex,
                    paymentDate: datesByIndex.TryGetValue(item.StageIndex, out var date) ? date : DateTimeOffset.MinValue,
                    amount: new AmountUI(item.Amount),
                    status: !string.IsNullOrEmpty(item.Status) ? item.Status : "Pending"
                )).ToList();
        }
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Flows.InvestV2.BackupWallet;
using AngorApp.UI.Flows.InvestV2.InvestmentResult;
using AngorApp.UI.Flows.InvestV2.Invoice;
using AngorApp.UI.Shell;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Flows.InvestV2.PaymentSelector
{
    public partial class PaymentSelectorViewModel : ReactiveObject, IPaymentSelectorViewModel, IHaveTitle
    {
        private readonly IShellViewModel shell;
        private readonly IInvestmentAppService investmentAppService;
        private readonly ProjectId projectId;
        private readonly UIServices uiServices;
        private readonly byte? patternIndex;

        [Reactive] private IWallet? selectedWallet;

        public PaymentSelectorViewModel(ProjectId projectId, UIServices uiServices, IShellViewModel shell, IInvestmentAppService investmentAppService, IWalletContext walletContext, IAmountUI amountToInvest, byte? patternIndex = null, IWallet? preSelectedWallet = null)
        {
            this.projectId = projectId;
            this.uiServices = uiServices;
            this.shell = shell;
            this.investmentAppService = investmentAppService;
            this.patternIndex = patternIndex;
            AmountToInvest = amountToInvest;

            Wallets = walletContext.Wallets;
            SelectedWallet = preSelectedWallet;
        }

        public IObservable<string> Title { get; } = Observable.Return("Select Payment Method");
        public IAmountUI AmountToInvest { get; }
        public IEnumerable<IWallet> Wallets { get; }

        public IEnumerable<IOption> Options(ICloseable closeable)
        {
            return
            [
                PayWithWalletOption(closeable),
                GenerateInvoiceOption(closeable)
            ];
        }

        private Option GenerateInvoiceOption(ICloseable closeable)
        {
            IEnhancedCommand<Unit> command = EnhancedCommand.Create(async () =>
            {
                closeable.Close();
                using var invoiceViewModel = new InvoiceViewModel(SelectedWallet!, investmentAppService, uiServices, AmountToInvest, projectId, shell, patternIndex);
                await uiServices.Dialog.Show(
                    invoiceViewModel,
                    "Pay Invoice to Invest",
                    (model, invoiceCloseable) =>
                    {
                        model.SetCloseable(invoiceCloseable);
                        return
                        [
                            new Option("Next",
                                       EnhancedCommand.Create(
                                           () =>
                                           {
                                               invoiceCloseable.Close();
                                               return uiServices.Dialog.Show(
                                                   new BackupWalletViewModel(uiServices),
                                                   "Backup Your Account",
                                                   (model, c) => model.Options(c, shell));
                                           },
                                           model.IsValid),
                                       new Settings { IsVisible = model.IsValid })
                        ];
                    });
            });

            return new Option("Generate Invoice Instead", command, new Settings());
        }

        private Option PayWithWalletOption(ICloseable closeable)
        {
            IObservable<string> title = this.WhenAnyValue(viewModel => viewModel.SelectedWallet)
                                            .Select(wallet => wallet.AsMaybe().Match(
                                                        x => "Pay with " + x.Name,
                                                        () => "Choose Wallet"));

            var payCommand = EnhancedCommand.CreateWithResult(() => PayFlow(closeable), this.WhenAnyValue(viewModel => viewModel.SelectedWallet).NotNull());
            return new Option(title, payCommand, new Settings());
        }

        private Task<Result> PayFlow(ICloseable closeable)
        {
            return Pay().Bind(async isAboveThreshold =>
            {
                closeable.Close();
                var title = isAboveThreshold ? "Investment Submitted" : "Investment Completed";
                var resultViewModel = new InvestResultViewModel(shell) { Amount = AmountToInvest, RequiresApproval = isAboveThreshold };
                await uiServices.Dialog.Show(resultViewModel, title, (model, c) => model.Options(c));
                return Result.Success();
            });
        }

        private async Task<Result<bool>> Pay()
        {
            var draftResult = await investmentAppService.BuildInvestmentDraft(
                new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                    SelectedWallet!.Id,
                    projectId,
                    new Amount(AmountToInvest.Sats),
                    new DomainFeerate(20),
                    PatternIndex: patternIndex));

            if (draftResult.IsFailure)
                return Result.Failure<bool>(draftResult.Error);

            var investmentDraft = draftResult.Value.InvestmentDraft;

            // Check if the investment is above the penalty threshold
            var thresholdResult = await investmentAppService.IsInvestmentAbovePenaltyThreshold(
                new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, new Amount(AmountToInvest.Sats)));

            if (thresholdResult.IsFailure)
                return Result.Failure<bool>(thresholdResult.Error);

            var isAboveThreshold = thresholdResult.Value.IsAboveThreshold;

            if (isAboveThreshold)
            {
                // Above threshold: Request founder signatures via SubmitInvestment
                var submitResult = await investmentAppService.SubmitInvestment(
                    new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                        SelectedWallet.Id,
                        projectId,
                        investmentDraft));

                if (submitResult.IsFailure)
                    return Result.Failure<bool>(submitResult.Error);
            }
            else
            {
                // Below threshold: Directly publish the transaction (no founder approval needed)
                var publishResult = await investmentAppService.SubmitTransactionFromDraft(
                    new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                        SelectedWallet.Id.Value,
                        projectId,
                        investmentDraft));

                if (publishResult.IsFailure)
                    return Result.Failure<bool>(publishResult.Error);
            }

            return Result.Success(isAboveThreshold);
        }
    }
}
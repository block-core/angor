using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
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

        [Reactive] private IWallet? selectedWallet;

        public PaymentSelectorViewModel(ProjectId projectId, UIServices uiServices, IShellViewModel shell, IInvestmentAppService investmentAppService, IWalletContext walletContext, IAmountUI amountToInvest, IWallet? preSelectedWallet = null)
        {
            this.projectId = projectId;
            this.uiServices = uiServices;
            this.shell = shell;
            this.investmentAppService = investmentAppService;
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
                var invoiceViewModel = new InvoiceViewModel(SelectedWallet!, investmentAppService, uiServices, AmountToInvest, projectId, shell);
                try
                {
                    await uiServices.Dialog.Show(
                        invoiceViewModel,
                        "Pay Invoice to Invest",
                        (model, closeable) =>
                        [
                            new Option(
                                "Next",
                                EnhancedCommand.Create(
                                    () =>
                                    {
                                        closeable.Close();
                                        return uiServices.Dialog.Show(
                                            new BackupWalletViewModel(uiServices),
                                            "Backup Your Account",
                                            (model, c) => model.Options(c, shell));
                                    },
                                    model.IsValid),
                                new Settings { IsVisible = model.IsValid })
                        ]);
                }
                finally
                {
                    invoiceViewModel.Dispose();
                }
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
            return Pay().Bind(async () =>
            {
                closeable.Close();
                await uiServices.Dialog.Show(new InvestResultViewModel(shell), "Investment Completed", (model, c) => model.Options(c));
                return Result.Success();
            });
        }

        private async Task<Result> Pay()
        {
            var draftResult = investmentAppService.BuildInvestmentDraft(
                new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                    SelectedWallet!.Id,
                    projectId,
                    new Amount(AmountToInvest.Sats),
                    new DomainFeerate(20)));

            var publishResult = await draftResult
                .Bind(response =>
                {
                    var publishAndStoreInvestorTransactionRequest = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(SelectedWallet.Id.Value, projectId, response.InvestmentDraft);        
                    return investmentAppService.SubmitTransactionFromDraft(publishAndStoreInvestorTransactionRequest);
                });
            
            return publishResult;
        }
    }
}
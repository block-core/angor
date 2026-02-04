using System.Linq;
using Angor.Sdk.Funding.Investor;
using AngorApp.UI.Flows.InvestV2.Invoice;
using AngorApp.UI.Flows.InvestV2.PaymentSelector;
using AngorApp.UI.Shell;
using Reactive.Bindings;
using Zafiro.Avalonia.Dialogs;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace AngorApp.UI.Flows.InvestV2.Footer
{
    public class FooterViewModel : IFooterViewModel
    {
        private readonly IFullProject fullProject;
        private readonly IInvestmentAppService investmentAppService;

        public FooterViewModel(
            IFullProject fullProject,
            IObservable<IAmountUI> amountToInvest,
            UIServices uiServices,
            IInvestmentAppService investmentAppService,
            IShellViewModel shell,
            IWalletContext walletContext
        )
        {
            this.fullProject = fullProject;
            this.investmentAppService = investmentAppService;
            AmountToInvest = new ReadOnlyReactiveProperty<IAmountUI>(amountToInvest);
            Invest = ReactiveCommand.CreateFromTask(
                () => InvestFlow(uiServices, shell, walletContext),
                AmountToInvest.Select(IsValidInvest)).Enhance();
        }

        public IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; }
        public IEnhancedCommand Invest { get; }
        public IAmountUI TotalRaised => fullProject.RaisedAmount;
        public int StageCount => fullProject.Stages.Count();

        private static bool IsValidInvest(IAmountUI ui)
        {
            return ui.Sats >= 10000;
        }

        private async Task<Result<Maybe<string>>> InvestFlow(
            UIServices uiServices,
            IShellViewModel shell,
            IWalletContext walletContext
        )
        {
            if (walletContext.Wallets.Count == 0)
            {
                return await walletContext.GetOrCreate().Map(wallet => uiServices.Dialog.ShowAndGetResult(
                                                                 new InvoiceViewModel(wallet),
                                                                 "Select Wallet",
                                                                 model => model.IsValid,
                                                                 _ => ""));
            }

            IWallet wallet = ChooseWallet(walletContext);

            if (HasEnoughBalance(wallet))
            {
                bool show = await uiServices.Dialog.Show(new InvoiceViewModel(wallet), "Select Wallet", _ => []);
                return Maybe.From(show ? "" : null);
            }

            return await uiServices.Dialog.ShowAndGetResult(
                new PaymentSelectorViewModel(fullProject.ProjectId, uiServices, shell, investmentAppService, walletContext, AmountToInvest.Value, wallet),
                "Select Wallet",
                (model, closeable) => model.Options(closeable),
                _ => "");
        }

        private bool HasEnoughBalance(IWallet wallet)
        {
            return wallet.Balance.Sats < AmountToInvest.Value.Sats;
        }

        private IWallet ChooseWallet(IWalletContext walletContext)
        {
            Maybe<IWallet> enoughBalance = walletContext.Wallets
                                                        .Where(w => w.Balance.Sats >= AmountToInvest.Value.Sats)
                                                        .OrderByDescending(w => w.CreatedOn)
                                                        .TryFirst();

            IWallet last = walletContext.Wallets.OrderByDescending(w => w.CreatedOn).Last();

            return enoughBalance.GetValueOrDefault(last);
        }
    }
}
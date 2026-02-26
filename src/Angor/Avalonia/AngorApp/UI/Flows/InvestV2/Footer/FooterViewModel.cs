using System.Linq;
using Angor.Sdk.Funding.Investor;
using Angor.Shared.Models;
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
        private readonly UIServices uiServices;
        private readonly IObservable<DynamicStagePattern?> selectedPattern;

        public FooterViewModel(
            IFullProject fullProject,
            IObservable<IAmountUI> amountToInvest,
            IObservable<DynamicStagePattern?> selectedPattern,
            UIServices uiServices,
            IInvestmentAppService investmentAppService,
            IShellViewModel shell,
            IWalletContext walletContext
        )
        {
            this.fullProject = fullProject;
            this.investmentAppService = investmentAppService;
            this.uiServices = uiServices;
            this.selectedPattern = selectedPattern;
            AmountToInvest = new ReadOnlyReactiveProperty<IAmountUI>(amountToInvest);
            
            // Compute stage count reactively: use pattern's StageCount for Fund/Subscribe, otherwise static stages
            StageCount = selectedPattern
                .Select(p => p != null && fullProject.ProjectType is ProjectType.Fund or ProjectType.Subscribe
                    ? p.StageCount
                    : fullProject.Stages.Count());
            
            // Compute penalty threshold status reactively as amount changes
            var penaltyThreshold = fullProject.PenaltyThreshold;
            IsAbovePenaltyThreshold = AmountToInvest
                .Select(amount => penaltyThreshold == null || amount.Sats > penaltyThreshold.Sats);
            
            Invest = ReactiveCommand.CreateFromTask(
                () => InvestFlow(uiServices, shell, walletContext),
                AmountToInvest.Select(IsValidInvest)).Enhance();
        }

        public IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; }
        public IEnhancedCommand Invest { get; }
        public IAmountUI TotalRaised => fullProject.RaisedAmount;
        public IObservable<int> StageCount { get; }
        public bool HasPenaltyThreshold => fullProject.PenaltyThreshold != null;
        
        public IObservable<bool> IsAbovePenaltyThreshold { get; }

        private static bool IsValidInvest(IAmountUI ui)
        {
            return ui.Sats >= 10000;
        }

        private byte? GetCurrentPatternIndex()
        {
            // Get the latest selected pattern value
            byte? patternIndex = null;
            selectedPattern.Take(1).Subscribe(p =>
            {
                if (p != null)
                    patternIndex = p.PatternId;
            });
            return patternIndex;
        }

        private async Task<Result<Maybe<string>>> InvestFlow(
            UIServices uiServices,
            IShellViewModel shell,
            IWalletContext walletContext
        )
        {
            var patternIndex = GetCurrentPatternIndex();

            if (walletContext.Wallets.Count == 0)
            {
                return await walletContext.GetOrCreate().Map(async wallet =>
                {
                    using var invoiceViewModel = new InvoiceViewModel(wallet, investmentAppService, uiServices, AmountToInvest.Value, fullProject.ProjectId, shell, patternIndex);
                    bool result = await uiServices.Dialog.Show(
                        invoiceViewModel,
                        "Select Wallet",
                        (model, closeable) =>
                        {
                            model.SetCloseable(closeable);
                            return [];
                        });
                    return Maybe.From(result ? "" : null);
                });
            }

            IWallet wallet = ChooseWallet(walletContext);

            if (HasEnoughBalance(wallet))
            {
                using var invoiceViewModel = new InvoiceViewModel(wallet, investmentAppService, uiServices, AmountToInvest.Value, fullProject.ProjectId, shell, patternIndex);
                bool show = await uiServices.Dialog.Show(invoiceViewModel, "Select Wallet", (model, closeable) =>
                {
                    model.SetCloseable(closeable);
                    return [];
                });
                return Maybe.From(show ? "" : null);
            }

            return await uiServices.Dialog.ShowAndGetResult(
                new PaymentSelectorViewModel(fullProject.ProjectId, uiServices, shell, investmentAppService, walletContext, AmountToInvest.Value, patternIndex, wallet),
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
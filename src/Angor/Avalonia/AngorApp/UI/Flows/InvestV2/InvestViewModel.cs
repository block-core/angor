using System.Linq;
using Angor.Sdk.Funding.Investor;
using AngorApp.UI.Flows.Invest.Amount;
using AngorApp.UI.Flows.InvestV2.Footer;
using AngorApp.UI.Flows.InvestV2.Header;
using AngorApp.UI.Flows.InvestV2.Model;
using AngorApp.UI.Shell;
using ReactiveUI.Validation.Helpers;
using Zafiro.Reactive;

namespace AngorApp.UI.Flows.InvestV2
{
    public partial class InvestViewModel : ReactiveValidationObject, IInvestViewModel, IValidatable
    {
        private readonly IFullProject fullProject;
        private readonly IShellViewModel shell;
        private readonly UIServices uiServices;
        private readonly IWalletContext walletContext;
        private readonly IInvestmentAppService investmentAppService;
        [Reactive] private IAmountUI? amountToInvest;
        
        public InvestViewModel(
            IFullProject fullProject,
            UIServices uiServices,
            IShellViewModel shell,
            IInvestmentAppService investmentAppService,
            IWalletContext walletContext
        )
        {
            this.fullProject = fullProject;
            this.uiServices = uiServices;
            this.shell = shell;
            this.investmentAppService = investmentAppService;
            this.walletContext = walletContext;

            StageBreakdowns = this.WhenAnyValue(model => model.AmountToInvest)
                                  .Select(amount => GetStageBreakdowns(fullProject, amount));
            Details = this.WhenAnyValue(model => model.AmountToInvest).Select(GetTransactionDetails);
            IsValid = this.WhenAnyValue(model => model.AmountToInvest).NotNull();
        }

        public string ProjectTitle => fullProject.Name;
        public decimal Progress => fullProject.RaisedAmount.Btc / fullProject.TargetAmount.Btc;
        public IAmountUI Raised => fullProject.RaisedAmount;

        public IObservable<IEnumerable<Breakdown>> StageBreakdowns { get; }
        public IObservable<TransactionDetails> Details { get; }
        public string ProjectId => fullProject.ProjectId.Value;

        public IEnumerable<IAmountUI> AmountPresets { get; } =
            [AmountUI.FromBtc(0.001), AmountUI.FromBtc(0.01), AmountUI.FromBtc(0.1), AmountUI.FromBtc(0.5)];

        public IObservable<object> Footer => Observable.Return(
            new FooterViewModel(
                fullProject,
                this.WhenAnyValue(model => model.AmountToInvest).Select(ui => ui ?? AmountUI.FromBtc(0)),
                uiServices,
                investmentAppService,
                shell,
                walletContext));

        public IObservable<object> Header => Observable.Return(new HeaderViewModel(fullProject));
        public IObservable<bool> IsValid { get; }

        private static IEnumerable<Breakdown> GetStageBreakdowns(IFullProject fullProject, IAmountUI? amount)
        {
            return fullProject.Stages.Select(stage => new Breakdown(
                                                 amount ?? AmountUI.FromBtc(0m),
                                                 stage.RatioOfTotal,
                                                 stage.ReleaseDate));
        }

        private static TransactionDetails GetTransactionDetails(IAmountUI? amount)
        {
            if (amount == null)
            {
                return TransactionDetails.Empty();
            }

            AmountUI angorFee = new((long)Math.Ceiling(amount.Sats * 0.01));
            return new TransactionDetails(amount, new AmountUI(0), angorFee);
        }
    }
}
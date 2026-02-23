using System.Linq;
using System.Reactive.Disposables;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.Model.Amounts;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell
{
    public partial class ShellViewModel : ReactiveObject, IShellViewModel, IDisposable
    {
        private readonly CompositeDisposable disposable = new();
        private readonly Dictionary<string, ISection> sectionsByName;
        private readonly IWalletContext walletContext;
        private readonly IInvestmentAppService investmentAppService;
        [Reactive] private IWallet? currentWallet;
        [Reactive] private bool isDarkThemeEnabled;
        [Reactive] private ISection selectedSection;
        [Reactive] private IAmountUI? totalInvested;

        public ShellViewModel(IEnumerable<ISection> sections, UIServices uiServices, IWalletContext walletContext, IInvestmentAppService investmentAppService)
        {
            this.walletContext = walletContext;
            this.investmentAppService = investmentAppService;
            sectionsByName = sections.ToDictionary(root => root.Name, root => root);
            SidebarSections =
            [
                sectionsByName["Home"],
                sectionsByName["Funds"],
                sectionsByName["Find Projects"],
                sectionsByName["Funded"],
                sectionsByName["My Projects"],
                sectionsByName["Funders"],
            ];

            SelectedSection = sectionsByName["Home"];
            GoToSettings = ReactiveCommand.Create(() => SelectedSection = sectionsByName["Settings"])
                                          .DisposeWith(disposable);
            IsDarkThemeEnabled = uiServices.IsDarkThemeEnabled;

            this.WhenAnyValue(x => x.IsDarkThemeEnabled)
                .BindTo(uiServices, services => services.IsDarkThemeEnabled)
                .DisposeWith(disposable);

            currentWallet = walletContext.Wallets.FirstOrDefault();

            walletContext.CurrentWalletChanges
                         .Select(maybe => maybe.GetValueOrDefault())
                         .BindTo(this, x => x.CurrentWallet)
                         .DisposeWith(disposable);

            // Fetch total invested when wallet changes
            this.WhenAnyValue(x => x.CurrentWallet)
                .Where(w => w != null)
                .SelectMany(w => Observable.FromAsync(() => LoadTotalInvested(w!.Id)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(amount => TotalInvested = amount)
                .DisposeWith(disposable);
        }

        private async Task<IAmountUI?> LoadTotalInvested(WalletId walletId)
        {
            var result = await investmentAppService.GetTotalInvested(
                new GetTotalInvested.GetTotalInvestedRequest(walletId));

            if (result.IsSuccess)
                return new AmountUI(result.Value.TotalInvestedSats);

            return null;
        }

        public void Dispose()
        {
            disposable.Dispose();
        }

        public ReactiveCommand<Unit, ISection> GoToSettings { get; set; }
        public IEnumerable<ISection> SidebarSections { get; }

        public IReadOnlyCollection<IWallet> Wallets => walletContext.Wallets;

        public void SetSection(string sectionName)
        {
            SelectedSection = sectionsByName[sectionName];
        }
    }
}
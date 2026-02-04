using System.Linq;
using System.Reactive.Disposables;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.UI.Shell
{
    public partial class ShellViewModel : ReactiveObject, IShellViewModel, IDisposable
    {
        private readonly CompositeDisposable disposable = new();
        private readonly Dictionary<string, ISection> sectionsByName;
        private readonly IWalletContext walletContext;
        [Reactive] private IWallet? currentWallet;
        [Reactive] private bool isDarkThemeEnabled;
        [Reactive] private ISection selectedSection;

        public ShellViewModel(IEnumerable<ISection> sections, UIServices uiServices, IWalletContext walletContext)
        {
            this.walletContext = walletContext;
            sectionsByName = sections.ToDictionary(root => root.Name, root => root);
            SidebarSections =
            [
                sectionsByName["Home"],
                sectionsByName["Funds"],
                sectionsByName["Find Projects"],
                sectionsByName["Funded"],
                sectionsByName["My Projects"]
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
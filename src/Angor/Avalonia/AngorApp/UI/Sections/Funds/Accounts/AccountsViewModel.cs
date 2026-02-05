using System.Reactive.Disposables;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using DynamicData;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountsViewModel : IAccountsViewModel, IDisposable
    {
        private readonly CompositeDisposable disposable = new();
        
        public AccountsViewModel(IWalletContext walletContext, WalletImportWizard walletImportWizard, UIServices uiServices)
        {
            walletContext.WalletChanges
                         .Group(wallet => wallet.ImportKind)
                         .Transform(IAccountGroup (g) => new AccountGroup(g, uiServices))
                         .Bind(out var accountGroups)
                         .Subscribe()
                         .DisposeWith(disposable);

            AccountGroups = accountGroups;
            ImportAccount = EnhancedCommand.CreateWithResult(walletImportWizard.Start).DisposeWith(disposable);
            
            walletContext.WalletChanges
                         .Group(wallet => wallet.NetworkKind)
                         .Transform(IAccountBalance (g) => new AccountBalance(g))
                         .Bind(out var accountBalances)
                         .Subscribe()
                         .DisposeWith(disposable);
            
            Balances = accountBalances;
        }

        public ICollection<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
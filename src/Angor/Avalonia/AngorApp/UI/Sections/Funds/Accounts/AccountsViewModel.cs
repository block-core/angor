using System.Reactive.Disposables;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using DynamicData;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountsViewModel : IAccountsViewModel, IDisposable
    {
        private readonly WalletImportWizard walletImportWizard;
        private readonly CompositeDisposable disposable = new();
        
        public AccountsViewModel(IWalletContext walletContext, WalletImportWizard walletImportWizard, UIServices uiServices)
        {
            this.walletImportWizard = walletImportWizard;
            walletContext.WalletChanges
                         .Group(wallet => wallet.ImportKind)
                         .Transform(IAccountGroup (g) => new AccountGroup(g, uiServices))
                         .Bind(out var accountGroups)
                         .Subscribe()
                         .DisposeWith(disposable);

            AccountGroups = accountGroups;
            ImportAccount = EnhancedCommand.CreateWithResult(DoImportAccount).DisposeWith(disposable);
            
            walletContext.WalletChanges
                         .Group(wallet => wallet.NetworkKind)
                         .Transform(IAccountBalance (g) => new AccountBalance(g))
                         .Bind(out var accountBalances)
                         .Subscribe()
                         .DisposeWith(disposable);
            
            Balances = accountBalances;
        }

        private async Task<Result> DoImportAccount()
        {
            await walletImportWizard.Start();
            // TODO:
            return Result.Success();
        }

        public IEnumerable<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand<Result> ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
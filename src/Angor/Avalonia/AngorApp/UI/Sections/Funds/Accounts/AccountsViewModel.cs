using System.Reactive.Disposables;
using Angor.Shared;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using Blockcore.Networks;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountsViewModel : IAccountsViewModel, IDisposable
    {
        private readonly CompositeDisposable disposable = new();
        
        public AccountsViewModel(IWalletContext walletContext, WalletImportWizard walletImportWizard, UIServices uiServices, INetworkConfiguration networkConfiguration)
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
            
            CanGetTestCoins = networkConfiguration.GetNetwork().NetworkType == NetworkType.Testnet;
            
            GetTestCoins = EnhancedCommand.Create(async () =>
            {
                var wallet = walletContext.CurrentWallet;
                if (wallet.HasValue && wallet.Value.CanGetTestCoins)
                {
                    await wallet.Value.GetTestCoins.Execute();
                }
            }).DisposeWith(disposable);
        }

        public ICollection<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }
        public IEnhancedCommand GetTestCoins { get; }
        public bool CanGetTestCoins { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
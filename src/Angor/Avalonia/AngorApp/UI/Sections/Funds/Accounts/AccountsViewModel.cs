using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using AngorApp.UI.Flows.AddWallet;
using Blockcore.Networks;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountsViewModel : IAccountsViewModel, IDisposable
    {
        private readonly CompositeDisposable disposable = new();
        private readonly IWalletContext walletContext;

        public AccountsViewModel(IWalletContext walletContext, IAddWalletFlow addWalletFlow, UIServices uiServices, INetworkConfiguration networkConfiguration, IWalletAppService walletAppService)
        {
            this.walletContext = walletContext;
            walletContext.WalletChanges
                         .Group(wallet => wallet.ImportKind)
                         .Transform(IAccountGroup (g) => new AccountGroup(g, uiServices))
                         .Bind(out var accountGroups)
                         .Subscribe()
                         .DisposeWith(disposable);

            AccountGroups = accountGroups;
            ImportAccount = EnhancedCommand.Create(async () => await addWalletFlow.Run()).DisposeWith(disposable);

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

            RefreshBalances = EnhancedCommand.Create(RefreshAllBalances).DisposeWith(disposable);
        }

        private async Task RefreshAllBalances()
        {
            foreach (var wallet in walletContext.Wallets)
            {
                await wallet.RefreshBalance.Execute();
            }
        }

        public ICollection<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }
        public IEnhancedCommand GetTestCoins { get; }
        public bool CanGetTestCoins { get; }
        public IEnhancedCommand RefreshBalances { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}

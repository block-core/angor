using System.Reactive.Disposables;
using Angor.Sdk.Common;
using DynamicData;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountGroup : IAccountGroup
    {
        private readonly CompositeDisposable disposable = new();

        public AccountGroup(IGroup<IWallet, WalletId, ImportKind> group, UIServices uiServices)
        {
            group.Cache.Connect()
                 .Transform(IAccount (wallet) => new Account(wallet, uiServices))
                 .Bind(out var accounts)
                 .Subscribe()
                 .DisposeWith(disposable);

            Accounts = accounts;
            Name = group.Key.ToString();
        }

        public IEnumerable<IAccount> Accounts { get; }
        public string Name { get; }
    }
}
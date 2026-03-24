using System.Collections.ObjectModel;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountsViewModelSample : IAccountsViewModel
    {
        public ICollection<IAccountGroup> AccountGroups { get; } =
        [
            new AccountGroupSample { Name = "Angor Accounts" },
            new AccountGroupSample { Name = "Imported Accounts" }
        ];

        public IEnhancedCommand ImportAccount { get; } = EnhancedCommand.Create(() => { });
        public IObservable<IAmountUI> TotalBalance { get; } = Observable.Return(AmountUI.FromBtc(0.4));

        public IEnumerable<IAccountBalance> Balances { get; } = new List<IAccountBalance>()
        {
            new AccountBalanceSample() { Name = "Bitcoin", Balance = Observable.Return(new AmountUI(100000000)) },
            new AccountBalanceSample() { Name = "Liquid", Balance = Observable.Return(new AmountUI(50000000)) },
            new AccountBalanceSample() { Name = "Lightning", Balance = Observable.Return(new AmountUI(4000000)) }
        };
        
        public IEnhancedCommand GetTestCoins { get; } = EnhancedCommand.Create(() => { });
            public bool CanGetTestCoins { get; } = true;
            public IEnhancedCommand RefreshBalances { get; } = EnhancedCommand.Create(() => { });
        }
}
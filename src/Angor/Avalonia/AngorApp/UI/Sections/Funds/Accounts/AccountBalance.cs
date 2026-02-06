using Angor.Sdk.Common;
using DynamicData;
using DynamicData.Aggregation;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountBalance : IAccountBalance
    {
        public AccountBalance(IGroup<IWallet, WalletId, NetworkKind> group)
        {
            Balance = group.Cache.Connect().Sum(wallet => wallet.Balance.Sats).Select(l => new AmountUI(l));
            Name = group.Key.ToString();
        }

        public string Name { get; }
        public IObservable<IAmountUI> Balance { get; }
    }
}
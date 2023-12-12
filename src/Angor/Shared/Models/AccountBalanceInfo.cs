namespace Angor.Shared.Models;

public class AccountBalanceInfo
{
    public long TotalBalance { get; set; }
    public long TotalUnconfirmedBalance { get; set; }


    public AccountInfo AccountInfo { get; private set; } = new ();
    public UnconfirmedInfo UnconfirmedInfo { get; private set; } = new ();

    public static AccountBalanceInfo GetBalance(AccountInfo account, UnconfirmedInfo unconfirmedInfo)
    {
        var balance = account.AddressesInfo.Concat(account.ChangeAddressesInfo).SelectMany(s => s.UtxoData).Sum(s => s.value);
        var balanceSpent = unconfirmedInfo.PendingSpent.Sum(s => s.value);

        var balanceUnconfirmed = unconfirmedInfo.PendingReceive.Sum(s => s.value);

        return new AccountBalanceInfo
        {
            TotalBalance = balance - balanceSpent,
            TotalUnconfirmedBalance = balanceUnconfirmed,
            AccountInfo = account,
            UnconfirmedInfo = unconfirmedInfo
        };
    }
}
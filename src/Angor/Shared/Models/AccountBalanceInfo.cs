namespace Angor.Shared.Models;

public class AccountBalanceInfo
{
    public long TotalBalance { get; set; }
    public long TotalUnconfirmedBalance { get; set; }

    public AccountInfo AccountInfo { get; private set; } = new ();
    public UnconfirmedInfo UnconfirmedInfo { get; private set; } = new ();

    public void CalculateBalance(AccountInfo account, UnconfirmedInfo unconfirmedInfo)
    {
        AccountInfo = account;
        UnconfirmedInfo = unconfirmedInfo;

        var balance = AccountInfo.AllAddresses().SelectMany(s => s.UtxoData).Sum(s => s.value);
        var balanceSpent = UnconfirmedInfo.AccountPendingSpent.Sum(s => s.value);

        var balanceUnconfirmed = UnconfirmedInfo.AccountPendingReceive.Sum(s => s.value);

        TotalBalance = balance - balanceSpent;
        TotalUnconfirmedBalance = balanceUnconfirmed;
    }
}
namespace Angor.Shared.Models;

public class AccountBalanceInfo
{
    public long TotalBalance { get; set; }
    public long TotalUnconfirmedBalance { get; set; }

    public AccountInfo AccountInfo { get; private set; } = new ();
    
    public List<UtxoData> AccountPendingReceive { get; set; } = new();

    public void UpdateAccountBalanceInfo(AccountInfo account, List<UtxoData> accountPendingReceive)
    {
        AccountInfo = account;
        AccountPendingReceive = accountPendingReceive;

        var balance = AccountInfo.AllAddresses().SelectMany(s => s.UtxoData)
            .Sum(s => s.value);
        
        var balanceSpent = AccountInfo.AllAddresses().SelectMany(s => s.UtxoData
                .Where(u => u.InMempoolTransaction))
            .Sum(s => s.value);

        var balanceUnconfirmed = AccountPendingReceive.Sum(s => s.value);

        TotalBalance = balance - balanceSpent;
        TotalUnconfirmedBalance = balanceUnconfirmed;
    }
}
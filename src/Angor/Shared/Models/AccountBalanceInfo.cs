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

        long balanceConfirmed = 0;
        long balanceUnconfirmed = 0;
        long balanceSpent = 0;

        foreach (var utxoData in AccountInfo.AllUtxos())
        {
            if (utxoData.PendingSpent)
            {
                balanceSpent += utxoData.value;
            }
            else if(utxoData.blockIndex > 0)
            {
                balanceConfirmed += utxoData.value;
            }
            else
            {
                balanceUnconfirmed += utxoData.value;
            }
        }

        balanceUnconfirmed += AccountPendingReceive.Sum(s => s.value);

        TotalBalance = balanceConfirmed;
        TotalUnconfirmedBalance = balanceUnconfirmed;
    }
}
namespace Angor.Shared.Models;

public class AccountBalanceInfo
{
    public long TotalBalance { get; set; }
    public long TotalUnconfirmedBalance { get; set; }
    public long TotalBalanceReserved { get; set; }

    public AccountInfo AccountInfo { get; private set; } = new ();
    
    public List<UtxoData> AccountPendingReceive { get; set; } = new();

    public void UpdateAccountBalanceInfo(AccountInfo account, List<UtxoData> accountPendingReceive)
    {
        AccountInfo = account;
        AccountPendingReceive = accountPendingReceive;

        long balanceConfirmed = 0;
        long balanceUnconfirmed = 0;
        long balanceSpent = 0;
        long balanceReserved = 0;

        foreach (var utxoData in AccountInfo.AllUtxos())
        {
            if (account.UtxoReservedForInvestment.Contains(utxoData.Outpoint.ToString()))
            {
                balanceReserved += utxoData.Value;
                continue;
            }

            if (utxoData.PendingSpent)
            {
                balanceSpent += utxoData.Value;
            }
            else if(utxoData.BlockIndex > 0)
            {
                balanceConfirmed += utxoData.Value;
            }
            else
            {
                balanceUnconfirmed += utxoData.Value;
            }
        }

        balanceUnconfirmed += AccountPendingReceive.Sum(s => s.Value);

        TotalBalance = balanceConfirmed;
        TotalUnconfirmedBalance = balanceUnconfirmed;
        TotalBalanceReserved = balanceReserved;
    }
}
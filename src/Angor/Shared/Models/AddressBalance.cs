namespace Angor.Shared.Models;

public class AddressBalance
{
    public string Address { get; set; }
    public long Balance { get; set; }
    public long TotalReceived { get; set; }
    public long TotalStake { get; set; }
    public long TotalMine { get; set; }
    public long TotalSent { get; set; }
    public int TotalReceivedCount { get; set; }
    public int TotalSentCount { get; set; }
    public int TotalStakeCount { get; set; }
    public int TotalMineCount { get; set; }
    public long PendingSent { get; set; }
    public long PendingReceived { get; set; }
}
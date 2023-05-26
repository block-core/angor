namespace Angor.Client.Shared.Models;

public class AddressBalance
{
    public string address { get; set; }
    public long balance { get; set; }
    public long totalReceived { get; set; }
    public long totalStake { get; set; }
    public long totalMine { get; set; }
    public long totalSent { get; set; }
    public int totalReceivedCount { get; set; }
    public int totalSentCount { get; set; }
    public int totalStakeCount { get; set; }
    public int totalMineCount { get; set; }
    public long pendingSent { get; set; }
    public long pendingReceived { get; set; }
}
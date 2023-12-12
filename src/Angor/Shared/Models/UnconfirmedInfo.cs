namespace Angor.Shared.Models;

public class UnconfirmedInfo
{
    public List<UtxoData> PendingReceive { get; set; } = new();
    public List<UtxoData> PendingSpent { get; set; } = new();
}
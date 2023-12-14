using Blockcore.Networks;

namespace Angor.Shared.Models;

public class UnconfirmedInfo
{
    public List<UtxoData> AccountPendingReceive { get; set; } = new();
    public List<UtxoData> AccountPendingSpent { get; set; } = new();

    public List<Outpoint> PendingSpent { get; set; } = new();

    public bool IsInPendingSpent(Outpoint outpoint)
    {
        return PendingSpent.Any(intput => intput.ToString() == outpoint.ToString());
    }

    public void RemoveInputFromPending(Outpoint outpoint)
    {
        foreach (var input in PendingSpent.ToList())
        {
            if (input.ToString() == outpoint.ToString())
            {
                PendingSpent.Remove(input);
            }
        }
    }

    public void AddInputsAsPending(Blockcore.Consensus.TransactionInfo.Transaction transaction)
    {
        var inputs = transaction.Inputs.Select(_ => _.PrevOut).ToList();

        foreach (var outPoint in inputs)
        {
            if (PendingSpent.All(input => input.ToString() != outPoint.ToString()))
            {
                PendingSpent.Add(Outpoint.FromOutPoint(outPoint));
            }
        }
    }
}
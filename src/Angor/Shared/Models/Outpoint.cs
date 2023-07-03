namespace Angor.Shared.Models;

public class Outpoint
{
    public string transactionId { get; set; }
    public int outputIndex { get; set; }

    public override string ToString()
    {
        return $"{transactionId}-{outputIndex}";
    }
}
namespace Angor.Shared.Models;

public class Outpoint 
{
    public string transactionId { get; set; }
    public int outputIndex { get; set; }

    public Outpoint(){ } //Required for JSON serializer
    
    public Outpoint(string trxid, int index)
    {
        outputIndex = index; transactionId = trxid;
    }
    
    public override string ToString()
    {
        return $"{transactionId}-{outputIndex}";
    }
}
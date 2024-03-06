namespace Angor.Shared.Models;

public class Outpoint 
{
    public string TransactionId { get; set; }
    public int OutputIndex { get; set; }

    public Outpoint(){ } //Required for JSON serializer
    
    public Outpoint(string trxid, int index)
    {
        OutputIndex = index; TransactionId = trxid;
    }
    
    public override string ToString()
    {
        return $"{TransactionId}-{OutputIndex}";
    }
}
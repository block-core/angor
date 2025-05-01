namespace Angor.UI.Model;

public interface IBroadcastedTransaction
{
    string Id { get; }
    public string RawJson { get; }
    public IAmountUI Balance { get; }
    public DateTimeOffset? BlockTime { get; }
}
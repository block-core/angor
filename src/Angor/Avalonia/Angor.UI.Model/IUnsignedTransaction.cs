using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IUnsignedTransaction
{
    public long TotalFee { get; set; }
    Task<Result<IBroadcastedTransaction>> Broadcast();
}
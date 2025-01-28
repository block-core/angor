using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IUnsignedTransaction
{
    public long TotalFee { get; set; }
    Task<Result<IBroadcastedTransaction>> Broadcast();
}
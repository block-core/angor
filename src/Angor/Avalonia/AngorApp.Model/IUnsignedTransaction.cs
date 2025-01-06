using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IUnsignedTransaction
{
    public ulong TotalFee { get; set; }
    Task<Result<IBroadcastedTransaction>> Broadcast();
}
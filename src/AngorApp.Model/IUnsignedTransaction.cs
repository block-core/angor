using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IUnsignedTransaction
{
    public decimal TotalFee { get; set; }
    Task<Result<IBroadcastedTransaction>> Broadcast();
}
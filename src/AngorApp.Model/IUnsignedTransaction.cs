using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IUnsignedTransaction
{
    Task<Result<IBroadcastedTransaction>> Broadcast();
}
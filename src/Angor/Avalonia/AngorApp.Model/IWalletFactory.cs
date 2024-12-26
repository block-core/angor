using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWalletFactory
{
    public Task<Result<IWallet>> Recover();
    public Task<Maybe<Result<IWallet>>> Create();
}
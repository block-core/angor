using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWalletFactory
{
    public Task<Maybe<Result<IWallet>>> Recover();
    public Task<Maybe<Result<IWallet>>> Create();
}
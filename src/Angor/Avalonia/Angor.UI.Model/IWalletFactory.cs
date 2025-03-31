using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IWalletFactory
{
    public Task<Maybe<Result<IWallet>>> Recover();
    public Task<Maybe<Result<IWallet>>> Create();
}
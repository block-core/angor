using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Domain;

public interface IWalletRepository
{
    Task<Result<Wallet>> Get(WalletId id);
}
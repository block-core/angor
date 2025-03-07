using CSharpFunctionalExtensions;

namespace Angor.Wallet.Domain;

public interface IWalletRepository
{
    Task<Result<Wallet>> Get(WalletId id);
}
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Domain;

public interface IWalletRepository
{
    Task<Result<IEnumerable<(WalletId Id, string Name)>>> ListWallets();
    Task<Result<Wallet>> Get(WalletId id);
}
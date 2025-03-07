using Angor.Wallet.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IWalletStore
{
    Task<Result<IEnumerable<EncryptedWallet>>> GetAll();
    Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets);
}
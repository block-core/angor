using Angor.Contexts.Wallet.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface IWalletStore
{
    Task<Result<IEnumerable<EncryptedWallet>>> GetAll();
    Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets);
}
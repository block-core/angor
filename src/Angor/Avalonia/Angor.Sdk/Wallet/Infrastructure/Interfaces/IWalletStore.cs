using Angor.Sdk.Wallet.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletStore
{
    Task<Result<IEnumerable<EncryptedWallet>>> GetAll();
    Task<Result> SaveAll(IEnumerable<EncryptedWallet> wallets);
}
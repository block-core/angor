using Angor.Wallet.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IWalletEncryption
{
    Task<Result<WalletData>> Decrypt(EncryptedWallet wallet, string encryptionKey);
    Task<EncryptedWallet> Encrypt(WalletData walletData, string encryptionKey, string name, Guid id);
}
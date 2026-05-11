using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletEncryption
{
    Task<Result<WalletData>> Decrypt(EncryptedWallet wallet, string encryptionKey);
    Task<EncryptedWallet> Encrypt(WalletData walletData, string encryptionKey, string name, string id);
}
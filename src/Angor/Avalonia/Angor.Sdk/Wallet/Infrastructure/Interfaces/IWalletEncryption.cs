using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletEncryption
{
    /// <summary>
    /// Encrypts wallet data using AES-GCM with a provided master key.
    /// </summary>
    Task<Result<EncryptedWallet>> EncryptAsync(
        WalletData walletData,
        string masterKey,
        string name,
        string id);

    /// <summary>
    /// Decrypts wallet data using AES-GCM with a provided master key.
    /// </summary>
    Task<Result<WalletData>> DecryptAsync(
        EncryptedWallet encryptedWallet,
        string masterKey);

    /// <summary>
    /// Encrypts wallet data, retrieving the master key automatically from secure storage.
    /// </summary>
    Task<Result<EncryptedWallet>> EncryptWithStoredKeyAsync(
        WalletData walletData,
        string walletId,
        string name);

    /// <summary>
    /// Decrypts wallet data, retrieving the master key automatically from secure storage.
    /// </summary>
    Task<Result<WalletData>> DecryptWithStoredKeyAsync(
        EncryptedWallet encryptedWallet);
}
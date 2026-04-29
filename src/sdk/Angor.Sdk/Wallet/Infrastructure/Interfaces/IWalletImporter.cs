using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletFactory
{
    Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, BitcoinNetwork network);
    Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network);
    Task<Result> RebuildFounderKeysAsync(WalletWords walletWords, WalletId walletId);
}
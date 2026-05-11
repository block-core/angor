using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletFactory
{
    Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, string? passphrase, BitcoinNetwork network);
    Task<Result> RebuildFounderKeysAsync(WalletWords walletWords, WalletId walletId);
}


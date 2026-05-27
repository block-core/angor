using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Operations;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Application;

public interface IWalletAppService
{
    Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas();
    Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId);
    Task<Result<Balance>> GetBalance(WalletId walletId);
    Task<Result<AccountBalanceInfo>> GetAccountBalanceInfo(WalletId walletId);
    Task<Result<AccountBalanceInfo>> RefreshAndGetAccountBalanceInfo(WalletId walletId);
    Task<Result<FeeAndSize>> EstimateFeeAndSize(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<Address>> GetNextReceiveAddress(WalletId id);
    Task<Result<IEnumerable<FeeEstimation>>> GetFeeEstimates();
    Task<Result<WalletId>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, BitcoinNetwork network);
    Task<Result<WalletId>> CreateWallet(string name, BitcoinNetwork network);
    public string GenerateRandomSeedwords();
    Task<Result> GetTestCoins(WalletId walletId);
    Task<Result> DeleteWallet(WalletId walletId);
    Task<Result> RebuildAllWalletBalancesAsync(WalletId? targetWalletId = null);

    /// <summary>Get the compressed public key hex for a receive address owned by this wallet.
    /// Used as the claim key for Boltz Lightning swaps.</summary>
    Task<Result<string>> GetPublicKeyForAddress(WalletId walletId, string address);

    /// <summary>Retrieve the seed words for an existing wallet (requires unlock/decryption).</summary>
    Task<Result<string>> GetSeedWords(WalletId walletId);

    /// <summary>Get all encrypted wallet entries from the wallet store (wallets.json).</summary>
    Task<Result<IEnumerable<GetStoredWallets.StoredWalletSummary>>> GetStoredWallets();

    /// <summary>Restore a wallet from the encrypted store using secure storage for decryption.</summary>
    Task<Result<WalletId>> RestoreStoredWallet(string walletId, string walletName, BitcoinNetwork network);

    /// <summary>Delete encrypted recovery wallet backups and optionally delete the wallet store file itself.</summary>
    Task<Result> DeleteRecoveryWalletFilesAsync(bool deleteWalletFile);
}

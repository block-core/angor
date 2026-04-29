using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
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
    Task<Result<WalletId>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network);
    Task<Result<WalletId>> CreateWallet(string name, BitcoinNetwork network);
    public string GenerateRandomSeedwords();
    Task<Result> GetTestCoins(WalletId walletId);
    Task<Result> DeleteWallet(WalletId walletId);
    Task<Result> RebuildAllWalletBalancesAsync();

    /// <summary>Get the compressed public key hex for a receive address owned by this wallet.
    /// Used as the claim key for Boltz Lightning swaps.</summary>
    Task<Result<string>> GetPublicKeyForAddress(WalletId walletId, string address);
}
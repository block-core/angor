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
    Task<Result<WalletId>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network);
    Task<Result<WalletId>> CreateWallet(string name, string encryptionKey, BitcoinNetwork network);
    /// <summary>Create a wallet without a user-provided password. Uses secure storage for the encryption key (placeholder: "DEFAULT").
    /// Callers must supply the current network since the SDK wallet layer doesn't own network configuration.</summary>
    Task<Result<WalletId>> CreateWalletWithoutPassword(BitcoinNetwork network);
    public string GenerateRandomSeedwords();
    Task<Result> GetTestCoins(WalletId walletId);
    Task<Result> DeleteWallet(WalletId walletId);
    Task<Result> RebuildAllWalletBalancesAsync();
}
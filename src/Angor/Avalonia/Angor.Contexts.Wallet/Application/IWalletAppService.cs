using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Application;

public interface IWalletAppService
{
    Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas();
    Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId);
    Task<Result<Balance>> GetBalance(WalletId walletId);
    Task<Result<FeeAndSize>> EstimateFeeAndSize(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<Address>> GetNextReceiveAddress(WalletId id);
    Task<Result<WalletId>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network);
    Task<Result<WalletId>> CreateWallet(string name, string encryptionKey, BitcoinNetwork network);
    public string GenerateRandomSeedwords();
    Task<Result> GetTestCoins(WalletId walletId);
    Task<Result> DeleteWallet(WalletId walletId);
}
using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Application;

public interface IWalletAppService
{
    Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<IEnumerable<(WalletId Id, string Name)>>> GetWallets();
    Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId);
    Task<Result<Balance>> GetBalance(WalletId walletId);
    Task<Result<Fee>> EstimateFee(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate);
    Task<Result<Address>> GetNextReceiveAddress(WalletId id);
    Task<Result<WalletId>> ImportWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network);
}
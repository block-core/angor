using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.History;

public interface ITransactionHistory
{
    Task<Result<IEnumerable<string>>> GetWalletAddresses(WalletId walletId);
    Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId);
}
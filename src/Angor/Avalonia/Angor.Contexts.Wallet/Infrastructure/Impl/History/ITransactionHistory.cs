using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.History;

public interface ITransactionHistory
{
    Task<Result<IEnumerable<string>>> GetWalletAddresses(WalletId walletId);
    Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId);
}
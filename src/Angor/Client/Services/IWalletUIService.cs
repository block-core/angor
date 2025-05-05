    using Angor.Shared.Models;
    using Blockcore.Consensus.TransactionInfo;

namespace Angor.Client.Services
{
    public interface IWalletUIService
    {
        /// <summary>
        /// Adds a transaction to the internal database of pending (unconfirmed) list of transactions.
        /// </summary>
        void AddTransactionToPending(Transaction transaction);

        /// <summary>
        /// Refreshes the wallet balance.
        /// </summary>
        Task<AccountBalanceInfo> RefreshWalletBalance(AccountBalanceInfo? accountBalanceInfo = null);
    }
}

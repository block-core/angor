using Blockcore.Consensus.TransactionInfo;

namespace Angor.Client.Services
{
    public interface IWalletUIService
    {
        void AddTransactionToPending(Transaction transaction);
    }
}

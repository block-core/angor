using Angor.Client.Storage;
using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Microsoft.Extensions.Logging;

namespace Angor.Client.Services
{
    public class WalletUIService : IWalletUIService
    {
        private readonly INetworkConfiguration _networkConfiguration;
        private readonly IClientStorage _storage;
        private readonly ICacheStorage _cacheStorage;
        private readonly IWalletOperations _walletOperations;
        private readonly ILogger<WalletUIService> _logger;

        public WalletUIService(INetworkConfiguration networkConfiguration, IClientStorage storage, ICacheStorage cacheStorage, IWalletOperations walletOperations, ILogger<WalletUIService> logger)
        {
            _networkConfiguration = networkConfiguration;
            _storage = storage;
            _cacheStorage = cacheStorage;
            _walletOperations = walletOperations;
            _logger = logger;
        }

        public void AddTransactionToPending(Transaction transaction)
        {
            var networkName = _networkConfiguration.GetNetwork().Name;
            var accountInfo = _storage.GetAccountInfo(networkName);
            var unconfirmedInbound = _cacheStorage.GetUnconfirmedInboundFunds();
            var unconfirmedOutbound = _cacheStorage.GetUnconfirmedOutboundFunds();

            unconfirmedInbound.AddRange(_walletOperations.UpdateAccountUnconfirmedInfoWithSpentTransaction(accountInfo, transaction));
            unconfirmedOutbound.AddRange(transaction.Inputs.Select(_ => new Outpoint(_.PrevOut.Hash.ToString(), (int)_.PrevOut.N)));

            _storage.SetAccountInfo(networkName, accountInfo);
            _cacheStorage.SetUnconfirmedInboundFunds(unconfirmedInbound);
            _cacheStorage.SetUnconfirmedOutboundFunds(unconfirmedOutbound);
        }
    }
}
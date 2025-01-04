using System;
using System.Linq;
using System.Threading.Tasks;
using Angor.Client.Storage;
using Angor.Shared;
using Microsoft.Extensions.Logging;

namespace Angor.Client.Services
{
    public class UTXOService : IUTXOService
    {
        private readonly IWalletStorage _walletStorage;
        private readonly IClientStorage _storage;
        private readonly ICacheStorage _cacheStorage;
        private readonly IWalletOperations _walletOperations;
        private readonly ILogger<UTXOService> _logger;
        private readonly INetworkConfiguration _networkConfiguration;

        public UTXOService(
            IWalletStorage walletStorage,
            IClientStorage storage,
            ICacheStorage cacheStorage,
            IWalletOperations walletOperations,
            ILogger<UTXOService> logger,
            INetworkConfiguration networkConfiguration)
        {
            _walletStorage = walletStorage ?? throw new ArgumentNullException(nameof(walletStorage));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
            _walletOperations = walletOperations ?? throw new ArgumentNullException(nameof(walletOperations));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        }

        public async Task RefreshUTXOsAsync()
        {
            try
            {
                var network = _networkConfiguration.GetNetwork();
                var accountInfo = _storage.GetAccountInfo(network.Name);
                var unconfirmedInboundFunds = _cacheStorage.GetUnconfirmedInboundFunds();

                _logger.LogInformation($"Refreshing UTXOs for network: {network.Name} ({network.CoinTicker})");

                await _walletOperations.UpdateDataForExistingAddressesAsync(accountInfo);
                await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

                _storage.SetAccountInfo(network.Name, accountInfo);

                // Remove spent UTXOs from unconfirmed cache
                var utxos = accountInfo.AllUtxos().Select(x => x.outpoint.ToString()).ToList();
                var spentToUpdate = unconfirmedInboundFunds.RemoveAll(x => utxos.Contains(x.outpoint.ToString()));

                if (spentToUpdate > 0)
                    _cacheStorage.SetUnconfirmedInboundFunds(unconfirmedInboundFunds);

                _logger.LogInformation("UTXOs refreshed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing UTXOs");
                throw;
            }
        }
    }
}

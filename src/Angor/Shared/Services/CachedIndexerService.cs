using Angor.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Angor.Shared.Services
{
    public class CachedIndexerService : ICachedIndexerService
    {
        private readonly IIndexerService _indexer;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public CachedIndexerService(IIndexerService indexerService, IMemoryCache cache)
        {
            _indexer = indexerService;
            _cache = cache;
        }

        public async Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl)
        {
            var cacheKey = $"indexer_network_{indexerUrl}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.CheckIndexerNetwork(indexerUrl);
            });
        }

        public async Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId = null)
        {
            var cacheKey = $"address_history_{address}_{afterTrxId ?? "null"}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.FetchAddressHistoryAsync(address, afterTrxId);
            });
        }

        public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
        {
            var cacheKey = $"utxo_{address}_{limit}_{offset}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.FetchUtxoAsync(address, limit, offset);
            });
        }

        public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false)
        {
            var addresses = string.Join(",", data.Select(d => d.Address).OrderBy(a => a));
            var cacheKey = $"address_balances_{addresses}_{includeUnconfirmed}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetAdressBalancesAsync(data, includeUnconfirmed);
            });
        }

        public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
        {
            var confirmationsKey = string.Join(",", confirmations.OrderBy(c => c));
            var cacheKey = $"fee_estimation_{confirmationsKey}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetFeeEstimationAsync(confirmations);
            });
        }

        public async Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey)
        {
            var cacheKey = $"investment_{projectId}_{investorPubKey}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetInvestmentAsync(projectId, investorPubKey);
            });
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var cacheKey = $"investments_{projectId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetInvestmentsAsync(projectId);
            });
        }

        public async Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId)
        {
            var cacheKey = $"project_{projectId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetProjectByIdAsync(projectId);
            });
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
        {
            var cacheKey = $"projects_{offset ?? 0}_{limit}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetProjectsAsync(offset, limit);
            });
        }

        public async Task<ProjectStats?> GetProjectStatsAsync(string projectId)
        {
            var cacheKey = $"project_stats_{projectId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetProjectStatsAsync(projectId);
            });
        }

        public async Task<string> GetTransactionHexByIdAsync(string transactionId)
        {
            var cacheKey = $"transaction_hex_{transactionId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetTransactionHexByIdAsync(transactionId);
            });
        }

        public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
        {
            var cacheKey = $"transaction_info_{transactionId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _indexer.GetTransactionInfoByIdAsync(transactionId);
            });
        }

        // Don't cache publishing transactions as this should always execute
        public async Task<string> PublishTransactionAsync(string trxHex)
        {
            return await _indexer.PublishTransactionAsync(trxHex);
        }

        public bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash)
        {
            // This is a pure function, no need for caching
            return _indexer.ValidateGenesisBlockHash(fetchedHash, expectedHash);
        }
    }
}
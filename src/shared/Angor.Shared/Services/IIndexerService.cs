using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface IIndexerService
{
    Task<string> PublishTransactionAsync(string trxHex);

    /// <summary>
    /// Best-effort broadcast with a cap on attempts (e.g. 1 for relaying a tx that is already broadcast elsewhere).
    /// Implementations that do not retry can rely on this default.
    /// </summary>
    Task<string> PublishTransactionAsync(string trxHex, int maxAttempts) => PublishTransactionAsync(trxHex);
    Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false);
    Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset);
    Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId = null);
    Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations);
    Task<string> GetTransactionHexByIdAsync(string transactionId);
    Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    Task<IEnumerable<(int index, bool spent)>> GetIsSpentOutputsOnTransactionAsync(string transactionId);
    Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl);
    bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash);
}
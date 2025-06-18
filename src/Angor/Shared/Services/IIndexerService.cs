using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface IIndexerService
{
    Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit);
    Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId);
    Task<(string projectId, ProjectStats? stats)> GetProjectStatsAsync(string projectId);
    Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
    Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey);
    
    
    Task<string> PublishTransactionAsync(string trxHex);
    Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false);
    Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset);
    Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId = null);
    Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations);

    Task<string> GetTransactionHexByIdAsync(string transactionId);

    Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl);
    bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash);
}
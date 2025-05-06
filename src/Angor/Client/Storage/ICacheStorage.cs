using Angor.Client.Models;
using Angor.Client.Services;
using Angor.Shared.Models;
using Angor.Shared.Services;

namespace Angor.Client.Storage;

public interface ICacheStorage
{
    void StoreProject(Project project);
    Project? GetProjectById(string projectId);
    bool IsProjectInStorageById(string projectId);
    ProjectMetadata? GetProjectMetadataByPubkey(string pubkey);
    bool IsProjectMetadataStorageByPubkey(string pubkey);
    List<ProjectIndexerData>? GetProjectIndexerData();
    void SetProjectIndexerData(List<ProjectIndexerData> list);
    List<UtxoData> GetUnconfirmedInboundFunds();
    List<Outpoint> GetUnconfirmedOutboundFunds();
    void SetUnconfirmedInboundFunds(List<UtxoData> unconfirmedInfo);
    void SetUnconfirmedOutboundFunds(List<Outpoint> unconfirmedInfo);
    void DeleteUnconfirmedInfo();
    void SetCurrencyRate(string currencyCode, RateCacheEntry rateCacheEntry);
    RateCacheEntry? GetCurrencyRate(string currencyCode);
    void WipeSession();
}
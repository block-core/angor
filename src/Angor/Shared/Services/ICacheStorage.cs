using Angor.Client.Services;
using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface ICacheStorage
{
    void StoreProjectInfo(ProjectInfo project);
    ProjectInfo? GetProjectById(string projectId);
    bool IsProjectInStorageById(string projectId);
    ProjectMetadata? GetProjectMetadataByPubkey(string pubkey);
    void StoreProjectMetadata(string pubkey, ProjectMetadata projectMetadata);
    bool IsProjectMetadataStorageByPubkey(string pubkey);
    List<ProjectIndexerData>? GetProjectIndexerData();
    void SetProjectIndexerData(List<ProjectIndexerData> list);
    List<UtxoData> GetUnconfirmedInboundFunds();
    List<Outpoint> GetUnconfirmedOutboundFunds();
    void SetUnconfirmedInboundFunds(List<UtxoData> unconfirmedInfo);
    void SetUnconfirmedOutboundFunds(List<Outpoint> unconfirmedInfo);

    void AddSubscriptionToEose(string subscriptionName);
    void AddEoseEventCalledOnClient(string subscriptionName, string communicatorName);
    List<string> GetNamesOfCommunicatorsThatReceivedEose(string subscriptionName);

    void DeleteUnconfirmedInfo();
    void WipeSession();
}
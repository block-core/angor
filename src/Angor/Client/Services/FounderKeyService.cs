using Angor.Shared.Models;       
using Angor.Shared.Services;     
using Nostr.Client.Messages;     
using Angor.Client.Storage;

public class FounderKeyService : IFounderKeyService
{
    private readonly IWalletStorage _walletStorage;
    private readonly IRelayService _relayService;
    private readonly ISerializer _serializer;
    



    public FounderKeyService(
        IWalletStorage walletStorage,
        IIndexerService indexerService,
        IRelayService relayService,
        IClientStorage storage,
        ISerializer serializer)
    {
        _walletStorage = walletStorage;
        _relayService = relayService;
        _serializer = serializer;
    }

    public async Task<bool> IsFounderKeyInUseAsync(string founderKey)
    {
        var keys = _walletStorage.GetFounderKeys();
        var inUseKeys = new HashSet<string>();

        await _relayService.RequestProjectCreateEventsByPubKeyAsync(
            keys.Keys.Select(k => k.NostrPubKey).ToArray(),
            eventMessage =>
            {
                if (eventMessage.Kind == NostrKind.ApplicationSpecificData)
                {
                    var projectInfo = _serializer.Deserialize<ProjectInfo>(eventMessage.Content);
                    inUseKeys.Add(projectInfo.FounderKey);
                }
            },
            null);

        return inUseKeys.Contains(founderKey);
    }
    
    public async Task<FounderKeyCheckResult> CheckFounderKeyAsync(string founderKey)
    {
        var keys = _walletStorage.GetFounderKeys();
        var usedKeys = new HashSet<string>();

        // Perform the relay scan to get project data
        await _relayService.RequestProjectCreateEventsByPubKeyAsync(
            keys.Keys.Select(k => k.NostrPubKey).ToArray(),
            eventMessage =>
            {
                if (eventMessage.Kind == NostrKind.ApplicationSpecificData)
                {
                    var projectInfo = _serializer.Deserialize<ProjectInfo>(eventMessage.Content);
                    usedKeys.Add(projectInfo.FounderKey);
                }
            },
            null);

        return new FounderKeyCheckResult
        {
            IsKeyInUse = usedKeys.Contains(founderKey),
            TotalKeysInUse = usedKeys.Count
        };
    }


}


public class FounderKeyCheckResult
{
    public bool IsKeyInUse { get; set; }
    public int TotalKeysInUse { get; set; }
}
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Primitives;

namespace Angor.Sdk.Common;

public class NetworkStorage(IStore store) : INetworkStorage
{
    private const string SettingsFile = "settings.json";

    private SettingsData? _settingsData;
    
    public SettingsInfo GetSettings()
    {
        var result = Load();
        if (result.IsFailure)
            return new SettingsInfo();

        var data = result.Value;
        return new SettingsInfo
        {
            Explorers = data.Explorers,
            Indexers = data.Indexers,
            Relays = data.Relays,
            ImageServers = data.ImageServers
        };
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
        var loadResult = Load();
        var data = loadResult.IsFailure ? new SettingsData() : loadResult.Value;
        data.Explorers = settingsInfo.Explorers;
        data.Indexers = settingsInfo.Indexers;
        data.Relays = settingsInfo.Relays;
        data.ImageServers = settingsInfo.ImageServers;
        store.Save(SettingsFile, data);
    }

    public void SetNetwork(string network)
    {
        var loadResult = Load();
        var data = loadResult.IsFailure ? new SettingsData() : loadResult.Value;
        data.Network = network;
        store.Save(SettingsFile, data);
    }

    public string GetNetwork()
    {
        var result = Load();
        if (result.IsFailure)
            return "Angornet";

        return result.Value.Network;
    }

    private Result<SettingsData> Load()
    {
        if (_settingsData != null)
        {
            return Result.Success(_settingsData);
        }

        var result = store.Load<SettingsData>(SettingsFile).GetAwaiter().GetResult();

        if (result.IsSuccess)
        {
            _settingsData = result.Value;
            return Result.Success(_settingsData);
        }
        else
        {
            _settingsData = null;
            return Result.Failure<SettingsData>(result.Error);
        }
    } 

    private class SettingsData
    {
        public string Network { get; set; } = "Angornet";
        public List<SettingsUrl> Explorers { get; set; } = new();
        public List<SettingsUrl> Indexers { get; set; } = new();
        public List<SettingsUrl> Relays { get; set; } = new();
        public List<SettingsUrl> ImageServers { get; set; } = new();
    }
}

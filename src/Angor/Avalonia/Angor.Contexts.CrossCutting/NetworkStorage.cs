using Angor.Shared;
using Angor.Shared.Models;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public class NetworkStorage(IStore store) : INetworkStorage
{
    private const string SettingsFile = "settings.json";

    public SettingsInfo GetSettings() => Load()
        .Map(data => new SettingsInfo
        {
            Explorers = data.Explorers,
            Indexers = data.Indexers,
            Relays = data.Relays
        })
        .OnFailureCompensate(_ => new SettingsInfo())
        .Value;

    public void SetSettings(SettingsInfo settingsInfo)
    {
        var data = Load().OnFailureCompensate(_ => new SettingsData()).Value;
        data.Explorers = settingsInfo.Explorers;
        data.Indexers = settingsInfo.Indexers;
        data.Relays = settingsInfo.Relays;
        store.Save(SettingsFile, data);
    }

    public void SetNetwork(string network)
    {
        var data = Load().OnFailureCompensate(_ => new SettingsData()).Value;
        data.Network = network;
        store.Save(SettingsFile, data);
    }

    public string GetNetwork() => Load()
        .Map(d => d.Network)
        .OnFailureCompensate(_ => "Angornet")
        .Value;

    private Result<SettingsData> Load() => store.Load<SettingsData>(SettingsFile).GetAwaiter().GetResult();

    private class SettingsData
    {
        public string Network { get; set; } = "Angornet";
        public List<SettingsUrl> Explorers { get; set; } = new();
        public List<SettingsUrl> Indexers { get; set; } = new();
        public List<SettingsUrl> Relays { get; set; } = new();
    }
}

using Angor.Shared;
using Angor.Shared.Models;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class NetworkStorage : INetworkStorage
{
    public SettingsInfo GetSettings()
    {
        return new SettingsInfo()
        {
            Indexers = new List<SettingsUrl>()
            {
                new SettingsUrl()
                {
                    Name = "test",
                    IsPrimary = true,
                    Url = "https://test.indexer.angor.io",
                }
            },
            Relays = new List<SettingsUrl>()
            {
                new SettingsUrl()
                {
                    Name = "relay",
                    IsPrimary = true,
                    Url = "wss://relay.angor.io",
                }
            } 
        };
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
    }

    public void SetNetwork(string network)
    {
        throw new NotImplementedException();
    }

    public string GetNetwork()
    {
        throw new NotImplementedException();
    }
}
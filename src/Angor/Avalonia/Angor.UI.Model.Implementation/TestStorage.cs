using Angor.Shared;
using Angor.Shared.Models;

namespace Angor.UI.Model.Implementation;

public class InMemoryStorage : INetworkStorage
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
                    Url = "https://mempool.thedude.pro",
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
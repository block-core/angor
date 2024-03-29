using Angor.Shared.Models;

namespace Angor.Shared;

public interface INetworkStorage
{
    SettingsInfo GetSettings();
    void SetSettings(SettingsInfo settingsInfo);

    public void SetNetwork(string network);
    public string GetNetwork();

}
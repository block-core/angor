using Angor.Shared.Models;

namespace Angor.Shared;

public interface INetworkStorage
{
    SettingsInfo GetSettings();
    void SetSettings(SettingsInfo settingsInfo);
}
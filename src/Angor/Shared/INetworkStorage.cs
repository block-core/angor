using Angor.Shared.Models;

namespace Angor.Shared;

public interface INetworkStorage
{
    SettingsInfo GetSettingsInfo();
    void SetSettingsInfo(SettingsInfo settingsInfo);
    void WipeStorage();
}
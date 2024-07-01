using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface INetworkService
{
    Task CheckServices(bool force = false);
    void AddSettingsIfNotExist();
    SettingsUrl GetPrimaryIndexer();
    SettingsUrl GetPrimaryRelay();
    List<SettingsUrl> GetRelays();
    void CheckAndHandleError(HttpResponseMessage httpResponseMessage);
    void HandleException(Exception exception);
    void CheckAndSetNetwork(string url, string? setNetwork = null);
    event Action OnStatusChanged;

}
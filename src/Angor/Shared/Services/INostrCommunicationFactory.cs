using Nostr.Client.Client;

namespace Angor.Shared.Services;

public interface INostrCommunicationFactory : IDisposable
{
    INostrClient CreateClient(INetworkService networkService);
    int GetNumberOfRelaysConnected();
}
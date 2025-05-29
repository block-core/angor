using Angor.Shared.Services;

namespace Angor.Client.Services;

public class WasmNostrEncryption : INostrEncryption
{
    private readonly IEncryptionService encryptionService;
    private readonly ISerializer serializer;

    public WasmNostrEncryption(IEncryptionService encryptionService, ISerializer serializer)
    {
        this.encryptionService = encryptionService;
        this.serializer = serializer;
    }

    public Task<string> Nip4Encryption<T>(T content, string localPrivateKey, string remotePublicKey)
    {
        return encryptionService.EncryptNostrContentAsync(localPrivateKey,remotePublicKey, serializer.Serialize(content));
    }
}
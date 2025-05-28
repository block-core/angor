using Nostr.Client.Messages;

public interface INostrEncryption
{
    Task<string> Nip44Encryption<T>(T content, string localPrivateKey, string remotePublicKey);
}
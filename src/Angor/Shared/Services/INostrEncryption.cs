using Nostr.Client.Messages;

public interface INostrEncryption
{
    Task<string> Nip4Encryption<T>(T content, string localPrivateKey, string remotePublicKey);
}
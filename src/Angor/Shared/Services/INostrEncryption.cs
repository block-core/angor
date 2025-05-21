using Nostr.Client.Messages;

public interface INostrEncryption
{
    Task<NostrEvent> Encrypt(NostrEvent ev, string localPrivateKey, string remotePublicKey);
}
namespace Angor.Shared.Services;

public class KeyIdentifier
{
    public KeyIdentifier(Guid walletId, string founderPubKey)
    {
        WalletId = walletId;
        FounderPubKey = founderPubKey;
    }

    public Guid WalletId { get; private set; }
    public string FounderPubKey { get; private set; }
}
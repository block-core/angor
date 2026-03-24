using Angor.Sdk.Common;

namespace Angor.Sdk.Wallet.Domain;

public class Wallet
{
    public Wallet(WalletId id, WalletDescriptor descriptor)
    {
        Id = id;
        Descriptor = descriptor;
    }

    public WalletId Id { get; }
    public WalletDescriptor Descriptor { get; }
}
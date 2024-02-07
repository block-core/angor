namespace Angor.Shared.Models;
public class Wallet
{
    public WalletWords WalletWords { get; set; } // todo: this will go away when we add passwords
    public FounderKeyCollection FounderKeys { get; set; }
    public WalletPayload Payload { get; set; }
}
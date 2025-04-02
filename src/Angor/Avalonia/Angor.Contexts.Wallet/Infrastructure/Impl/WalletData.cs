namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletData
{
    public string? DescriptorJson { get; set; }
    public bool RequiresPassphrase { get; set; }
    public string? SeedWords { get; set; }
}
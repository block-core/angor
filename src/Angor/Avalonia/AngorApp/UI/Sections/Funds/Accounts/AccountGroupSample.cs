using AngorApp.UI.Shared.Samples;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountGroupSample : IAccountGroup
    {
        public IEnumerable<IAccount> Accounts { get; set; } =
        [
            new AccountSample() { Wallet = new WalletSample() { Name = "Bitcoin Wallet" } },
            new AccountSample() { Wallet = new WalletSample { Name = "Liquid Wallet", NetworkKind = NetworkKind.Liquid } },
            new AccountSample() { Wallet = new WalletSample { Name = "Lightning Wallet", NetworkKind = NetworkKind.Lightning } }
        ];

        public string Name { get; set; } = "Default";
    }
}
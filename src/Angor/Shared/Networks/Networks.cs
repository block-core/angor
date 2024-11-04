using Blockcore.Networks;

namespace Angor.Shared.Networks
{
    public static class Networks
    {
        public static AngorNetworksSelector Bitcoin
        {
            get
            {
                return new AngorNetworksSelector(() => new BitcoinMain(), () => new BitcoinTest(), () => null);
            }
        }

     
    }

    public class AngorNetworksSelector
    {
        public AngorNetworksSelector(Func<Network> mainnet, Func<Network> testnet, Func<Network> regtest)
        {
            this.Mainnet = mainnet;
            this.Testnet = testnet;
            this.Regtest = regtest;
        }

        public Func<Network> Mainnet { get; }

        public Func<Network> Testnet { get; }

        public Func<Network> Testnet4 { get; }

        public Func<Network> Regtest { get; }

        public Func<Network> AngorNet { get; }

        public Func<Network> SigNet { get; }

        public Func<Network> MutinyNet { get; }
    }
}
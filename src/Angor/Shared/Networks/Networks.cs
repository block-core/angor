using Blockcore.Networks;

namespace Angor.Shared.Networks
{
    public static class Networks
    {
        public static AngorNetworksSelector Bitcoin
        {
            get
            {
                return new AngorNetworksSelector(
                    () => new BitcoinMain(),
                    () => new BitcoinTest(), 
                    () => null,
                    () => new BitcoinTest4(), 
                    () => new BitcoinSignet(),
                    () => new Angornet(),
                    () => null,
                    () => null);
            }
        }
    }

    public class AngorNetworksSelector
    {
        public AngorNetworksSelector(
            Func<Network> mainnet, 
            Func<Network> testnet, 
            Func<Network> regtest, 
            Func<Network> testnet4, 
            Func<Network> signet, 
            Func<Network> angornet, 
            Func<Network> mutinynet, 
            Func<Network> liquidnet)
        {
            this.Mainnet = mainnet;
            this.Testnet = testnet;
            this.Regtest = regtest;
            this.AngorNet = angornet;
            this.LiquidNet = liquidnet;
            this.MutinyNet = mutinynet;
            this.SigNet = signet;
            this.Testnet4 = testnet4;
        }

        public Func<Network> Mainnet { get; }

        public Func<Network> Testnet { get; }

        public Func<Network> Testnet4 { get; }

        public Func<Network> Regtest { get; }

        public Func<Network> AngorNet { get; }

        public Func<Network> SigNet { get; }

        public Func<Network> MutinyNet { get; }

        public Func<Network> LiquidNet { get; }


        public static Network NetworkByName(string networkName)
        {
            if (networkName == "Angornet")
            {
                return new Angornet();
            }

            if (networkName == "Main")
            {
                return new BitcoinMain();
            }

            throw new ApplicationException($"The network '{networkName}' is not recognized");
        }
    }
}
using Angor.Primitives.Network;

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
                    () => new LiquidMain());
            }
        }
    }

    public class AngorNetworksSelector
    {
        public AngorNetworksSelector(
            Func<AngorNetwork> mainnet,
            Func<AngorNetwork> testnet,
            Func<AngorNetwork> regtest,
            Func<AngorNetwork> testnet4,
            Func<AngorNetwork> signet,
            Func<AngorNetwork> angornet,
            Func<AngorNetwork> mutinynet,
            Func<AngorNetwork> liquidnet)
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

        public Func<AngorNetwork> Mainnet { get; }

        public Func<AngorNetwork> Testnet { get; }

        public Func<AngorNetwork> Testnet4 { get; }

        public Func<AngorNetwork> Regtest { get; }

        public Func<AngorNetwork> AngorNet { get; }

        public Func<AngorNetwork> SigNet { get; }

        public Func<AngorNetwork> MutinyNet { get; }

        public Func<AngorNetwork> LiquidNet { get; }


        public static AngorNetwork NetworkByName(string networkName)
        {
            return networkName switch
            {
                "Angornet" => new Angornet(),
                "Main" => new BitcoinMain(),
                "Liquid" => new LiquidMain(),
                _ => new BitcoinMain() // Default fallback
            };
        }
    }
}
using Blockcore.Networks;

namespace Angor.Shared.Networks
{
    public static class Networks
    {
        public static NetworksSelector Bitcoin
        {
            get
            {
                return new NetworksSelector(() => new BitcoinMain(), () => null, () => null);
            }
        }

     
    }
}
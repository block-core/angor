using Angor.Client.Shared.Models;

namespace Angor.Client.Storage
{
    public interface IClientStorage
    {

        public void SetWalletPubkey(string pubkey);
        AccountInfo GetAccountInfo(string network);
        public void SetAccountInfo(string network, AccountInfo items);
        public void DeleteAccountInfo(string network);
    }
}

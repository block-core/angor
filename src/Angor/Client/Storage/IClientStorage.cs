using Angor.Shared.Models;

namespace Angor.Client.Storage
{
    public interface IAccountStorage
    {
        AccountInfo GetAccountInfo(string network);
        void SetAccountInfo(string network, AccountInfo items);
        void DeleteAccountInfo(string network);
    }
}

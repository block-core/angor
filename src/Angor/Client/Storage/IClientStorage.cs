using Angor.Shared.Models;

namespace Angor.Client.Storage
{
    public interface IClientStorage
    {

        void SetWalletPubkey(string pubkey);
        string? GetWalletPubkey();
        void DeleteWalletPubkey();
        AccountInfo GetAccountInfo(string network);
        void SetAccountInfo(string network, AccountInfo items);
        void DeleteAccountInfo(string network);
        void AddProject(ProjectInfo project);
        List<ProjectInfo> GetProjects();
        void AddFounderProject(ProjectInfo project);
        List<ProjectInfo> GetFounderProjects();
    }
}

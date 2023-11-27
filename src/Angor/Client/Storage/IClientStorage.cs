using Angor.Client.Models;
using Angor.Shared.Models;

namespace Angor.Client.Storage
{
    public interface IClientStorage
    {
        void SetFounderKeys(FounderKeyCollection founderPubKeys);
        FounderKeyCollection GetFounderKeys();
        void DeleteFounderKeys();
        string? GetWalletPubkey();
        void DeleteWalletPubkey();
        AccountInfo GetAccountInfo(string network);
        void SetAccountInfo(string network, AccountInfo items);
        void DeleteAccountInfo(string network);
        void AddInvestmentProject(ProjectInfo project);
        void UpdateInvestmentProject(ProjectInfo project);
        List<ProjectInfo> GetInvestmentProjects();
        void AddFounderProject(params FounderProject[] projects);
        List<FounderProject> GetFounderProjects();
        void UpdateFounderProject(FounderProject project);
        void AddOrUpdateSignatures(SignatureInfo signatureInfo);
        List<SignatureInfo> GetSignaturess();
        SettingsInfo GetSettingsInfo();
        void SetSettingsInfo(SettingsInfo settingsInfo);
    }
}

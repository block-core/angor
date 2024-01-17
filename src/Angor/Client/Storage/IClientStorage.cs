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
        void AddInvestmentProjectMetadata(string pubkey, ProjectMetadata projectMetadata);
        Dictionary<string, ProjectMetadata> GetInvestmentProjectsMetadata();
        void DeleteInvestmentProjects();
        void AddFounderProject(params FounderProject[] projects);
        List<FounderProject> GetFounderProjects();
        void UpdateFounderProject(FounderProject project);
        void DeleteFounderProjects();
        void AddOrUpdateSignatures(SignatureInfo signatureInfo);
        List<SignatureInfo> GetSignatures();
        void DeleteSignatures();
        SettingsInfo GetSettingsInfo();
        void SetSettingsInfo(SettingsInfo settingsInfo);
        void WipeStorage();
    }
}

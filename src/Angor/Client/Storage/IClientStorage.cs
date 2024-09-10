using Angor.Client.Models;
using Angor.Shared.Models;

namespace Angor.Client.Storage
{
    public interface IClientStorage
    {
        AccountInfo GetAccountInfo(string network);
        void SetAccountInfo(string network, AccountInfo items);
        void DeleteAccountInfo(string network);
        
        
        void AddInvestmentProject(InvestorProject project);
        void RemoveInvestmentProject(string projectId);
        void UpdateInvestmentProject(InvestorProject project);
        List<InvestorProject> GetInvestmentProjects();
        
        
        void AddFounderProject(params FounderProject[] projects);
        void ClearFounderProjects();
        List<FounderProject> GetFounderProjects();
        FounderProject? GetFounderProjects(string projectIdentifier);
        void UpdateFounderProject(FounderProject project);
        void DeleteFounderProjects();
        
        
        // void AddOrUpdateSignatures(SignatureInfo signatureInfo);
        // void RemoveSignatures(SignatureInfo signatureInfo);
        // List<SignatureInfo> GetSignatures();
        // void DeleteSignatures();
        void SetNostrPublicKeyPerProject(string projectId, string nostrPubKey);
        string GetNostrPublicKeyPerProject(string projectId);
        
        SettingsInfo GetSettingsInfo();
        void SetSettingsInfo(SettingsInfo settingsInfo);
        void WipeStorage();
    }
}

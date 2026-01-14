using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IProjectDeploymentOrchestrator
    {
        Task<Result<string>> Deploy(WalletId walletId, CreateProjectDto dto, ProjectSeedDto projectSeed);
    }
}
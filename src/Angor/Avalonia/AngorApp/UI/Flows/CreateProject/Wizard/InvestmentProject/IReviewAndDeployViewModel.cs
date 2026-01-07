using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IReviewAndDeployViewModel
    {
        IInvestmentProjectConfig NewProject { get; }
        IEnhancedCommand<Result<string>> DeployCommand { get; }
    }
}
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ReviewAndDeployViewModelSample : IReviewAndDeployViewModel
    {
        public IInvestmentProjectConfig NewProject { get; set; } = new InvestmentProjectConfigSample();
        public IEnhancedCommand<Result<string>> DeployCommand { get; } = ReactiveCommand.Create(() => Result.Success("SampleTransactionId")).Enhance();
    }
}
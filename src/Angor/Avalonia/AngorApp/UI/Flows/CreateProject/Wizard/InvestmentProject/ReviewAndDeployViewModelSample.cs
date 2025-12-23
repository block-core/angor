using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ReviewAndDeployViewModelSample : IReviewAndDeployViewModel
    {
        public INewProject NewProject { get; set; } = new NewProjectSample();
    }
}
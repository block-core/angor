using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public class NewProjectSample : INewProject
    {
        public string Name { get; set; } = "New Project";
        public string Description { get; set; } = "New project's description";
        public string Website { get; set; } = "https://example.com";

        public IEnumerable<ICreateStage> Stages { get; set; } =
        [
            new CreateStageSample(),
            new CreateStageSample(),
            new CreateStageSample()
        ];
    }
}
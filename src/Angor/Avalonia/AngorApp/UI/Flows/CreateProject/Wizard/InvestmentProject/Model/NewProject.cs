namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public interface INewProject
    {
        string Name { get; set; }
        string Description { get; set; }
        string Website { get; set; }
        IEnumerable<ICreateStage> Stages { get; }
    }

    public class NewProject : INewProject
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
        public IEnumerable<ICreateStage> Stages { get; }
    }

    public interface ICreateStage
    {
        public double Percent { get; }
        public DateTime ReleaseDate { get; }
        public IAmountUI Amount { get; }
    }

    public class CreateStage : ICreateStage
    {
        public double Percent { get; }
        public DateTime ReleaseDate { get; }
        public IAmountUI Amount { get; }
    }
}

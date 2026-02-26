namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public interface IInvestmentProject : IProject
    {
        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<IEnumerable<IStage>> Stages { get; }
    }
}
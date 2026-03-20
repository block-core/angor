namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public sealed record PeriodOption
    {
        public string Title { get; init; } = string.Empty;
        public int Value { get; init; }
        public PeriodUnit Unit { get; init; }
    }
}

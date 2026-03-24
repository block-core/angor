namespace AngorApp.Model.ProjectsV2.InvestmentProject
{
    public interface IStage
    {
        public string Status { get; }
        public int Id { get; }
        public decimal Ratio { get; }
        public DateTimeOffset ReleaseDate { get; }
        public IAmountUI Total { get; }
        public IAmountUI Amount { get; }
    }
}

using AngorApp.Model.ProjectsV2.InvestmentProject;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

namespace AngorApp.Model.Funded.Investment.Samples
{
    public class StageSample : IStage
    {
        public StageStatus Status { get; } = StageStatus.Pending;
        public int Id { get; } = 1;
        public decimal Ratio { get; } = 0.3m;
        public DateTimeOffset ReleaseDate { get; } = DateTimeOffset.Now;
        public IAmountUI Total { get; } = AmountUI.FromBtc(1m);
    }
}
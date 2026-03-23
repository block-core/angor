using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

namespace AngorApp.Model.Funded.Investment.Model
{
    public interface IInvestmentFunded : IFunded
    {
        new IInvestmentProject Project { get; }
        new IInvestmentInvestorData InvestorData { get; }
        IObservable<IReadOnlyCollection<IStage>> StagesWithStatus { get; }
    }
}

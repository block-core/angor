using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

namespace AngorApp.Model.Funded.Investment.Model
{
    public class InvestmentFundedSample : IInvestmentFunded
    {
        public InvestmentFundedSample() : this(new InvestmentInvestorDataSample())
        {
            InvestorData = new InvestmentInvestorDataSample();
        }

        public InvestmentFundedSample(IInvestmentInvestorData investorData)
        {
            InvestorData = investorData;
        }

        public IInvestmentProject Project { get; } = new InvestmentProjectSample();
        public IInvestmentInvestorData InvestorData { get; }
        public IObservable<IReadOnlyCollection<IStage>> StagesWithStatus => Project.Stages;

        public IEnhancedCommand<Result> CancelApproval { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> CancelInvestment { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> ConfirmInvestment { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> OpenChat { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> RecoverFunds { get; } = EnhancedCommand.CreateWithResult(Result.Success);

        IProject IFunded.Project => Project;
        IInvestorData IFunded.InvestorData => InvestorData;
    }
}

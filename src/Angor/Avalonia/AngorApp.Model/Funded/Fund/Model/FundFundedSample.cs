using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;

namespace AngorApp.Model.Funded.Fund.Model
{
    public class FundFundedSample : IFundFunded
    {
        public FundFundedSample() : this(new FundInvestorDataSample())
        {
        }

        public FundFundedSample(IFundInvestorData investorData)
        {
            InvestorData = investorData;
        }

        public IFundProject Project { get; } = new FundProjectSample();
        public IFundInvestorData InvestorData { get; }

        public IEnhancedCommand<Result> CancelApproval { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> CancelInvestment { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> ConfirmInvestment { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> OpenChat { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand<Result> RecoverFunds { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IObservable<string> RecoverFundsLabel { get; } = Observable.Return("Recover Funds");
        public IObservable<IReadOnlyCollection<IPayment>> PaymentsWithStatus => Project.Payments;

        IProject IFunded.Project => Project;
        IInvestorData IFunded.InvestorData => InvestorData;
    }
}
using AngorApp.Model.ProjectsV2;

namespace AngorApp.Model.Funded.Shared.Model
{
    public interface IFunded
    {
        public IProject Project { get; }
        public IInvestorData InvestorData { get; }
        public IEnhancedCommand<Result> CancelApproval { get; }
        public IEnhancedCommand<Result> CancelInvestment { get; }
        public IEnhancedCommand<Result> ConfirmInvestment { get; }
        public IEnhancedCommand<Result> OpenChat { get; }
        public IEnhancedCommand<Result> RecoverFunds { get; }
    }
}
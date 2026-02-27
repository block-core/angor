using AngorApp.UI.Sections.FundedV2.Common.Model;

namespace AngorApp.UI.Sections.FundedV2.Host.Manage
{
    internal interface IManageViewModel
    {
        public IFunded Funded { get; }
        public IEnhancedCommand<Result> CancelApproval { get; }
        public IEnhancedCommand<Result> OpenChat { get; }
        public IEnhancedCommand<Result> CancelInvestment { get; }
        public IEnhancedCommand<Result> ConfirmInvestment { get; }
    }
}
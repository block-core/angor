namespace AngorApp.UI.Sections.Funded.Manage;

public interface IManageViewModel
{
    IFundedProject Project { get; }
    public IEnhancedCommand<Result> CancelApproval { get; }
    public IEnhancedCommand<Result> OpenChat { get; }
    public IEnhancedCommand<Result> CancelInvestment { get; }
    public IEnhancedCommand<Result> ConfirmInvestment { get; }
}

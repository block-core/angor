namespace AngorApp.Model.Funded.Shared.Model;

public interface IFundedCommands : IDisposable
{
    IEnhancedCommand<Result> CancelApproval { get; }
    IEnhancedCommand<Result> CancelInvestment { get; }
    IEnhancedCommand<Result> ConfirmInvestment { get; }
    IEnhancedCommand<Result> OpenChat { get; }
    IEnhancedCommand<Result> RecoverFunds { get; }
    IObservable<string> RecoverFundsLabel { get; }
}

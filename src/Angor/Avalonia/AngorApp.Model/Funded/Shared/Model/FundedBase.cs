using System.Reactive.Disposables;
using AngorApp.Model.ProjectsV2;
using ReactiveUI;
using Zafiro.Reactive;

namespace AngorApp.Model.Funded.Shared.Model;

public abstract class FundedBase : IFunded, IDisposable
{
    private readonly CompositeDisposable disposables = new();
    private readonly IFundedCommands commands;

    protected FundedBase(
        IProject project,
        IInvestorData investorData,
        IFundedCommandsFactory fundedCommandsFactory
    )
    {
        Project = project;
        InvestorData = investorData;
        commands = fundedCommandsFactory.Create(project, investorData);

        OpenChat = commands.OpenChat;
        CancelApproval = commands.CancelApproval;
        CancelInvestment = commands.CancelInvestment;
        ConfirmInvestment = commands.ConfirmInvestment;
        RecoverFunds = commands.RecoverFunds;
        RecoverFundsLabel = commands.RecoverFundsLabel;

        var refreshHappened = CancelApproval.Merge(CancelInvestment).Merge(ConfirmInvestment).Merge(RecoverFunds).ToSignal();

        refreshHappened.InvokeCommand(InvestorData.Refresh).DisposeWith(disposables);
    }

    public IProject Project { get; }
    public IInvestorData InvestorData { get; }
    public IEnhancedCommand<Result> CancelApproval { get; }
    public IEnhancedCommand<Result> CancelInvestment { get; }
    public IEnhancedCommand<Result> ConfirmInvestment { get; }
    public IEnhancedCommand<Result> OpenChat { get; }
    public IEnhancedCommand<Result> RecoverFunds { get; }
    public IObservable<string> RecoverFundsLabel { get; }

    public void Dispose()
    {
        commands.Dispose();
        disposables.Dispose();
    }
}

using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using AngorApp.Core;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;
    private readonly UIServices uiServices;

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, IWalletContext walletContext, SharedCommands sharedCommands)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;

        Load = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(wallet => GetRecoveryStateViewModel(wallet, sharedCommands))).Enhance().DisposeWith(disposables);
        State = Load.Successes();
        
        // Refresh on Batch Action completion
        State.Select(model => model.BatchAction).Switch().ToSignal().InvokeCommand(Load).DisposeWith(disposables);
    }

    public IObservable<RecoveryStateViewModel> State { get; }

    private Task<Result<RecoveryStateViewModel>> GetRecoveryStateViewModel(IWallet wallet, SharedCommands sharedCommands)
    {
        return investmentAppService
            .GetInvestorProjectRecovery(wallet.Id.Value, projectId)
            .Map(dto => new RecoveryStateViewModel(wallet.Id, dto, sharedCommands, investmentAppService, uiServices));
    }

    public IEnhancedCommand ViewTransaction { get; }
    public IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
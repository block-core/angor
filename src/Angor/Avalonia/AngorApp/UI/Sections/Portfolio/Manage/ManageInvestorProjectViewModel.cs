using System.Reactive.Disposables;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using AngorApp.Core;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.Portfolio.Manage;

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

        Load = ReactiveCommand.CreateFromTask(() => walletContext.Require().Bind(wallet => GetRecoveryStateViewModel(wallet, sharedCommands))).Enhance().DisposeWith(disposables);
        Load.HandleErrorsWith(uiServices.NotificationService);
        State = Load.Successes();
        
        // Refresh on Batch Action completion
        State.Select(model => model.BatchAction).Switch().ToSignal().InvokeCommand(Load).DisposeWith(disposables);
    }

    public IObservable<RecoveryStateViewModel> State { get; }

    private Task<Result<RecoveryStateViewModel>> GetRecoveryStateViewModel(IWallet wallet, SharedCommands sharedCommands)
    {
        return investmentAppService
       .GetRecoveryStatus(new GetRecoveryStatus.GetRecoveryStatusRequest(wallet.Id, projectId))
            .Map(response => new RecoveryStateViewModel(wallet.Id, response.RecoveryData, sharedCommands, investmentAppService, uiServices));
  }

    public IEnhancedCommand ViewTransaction { get; } = null!;
    public IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
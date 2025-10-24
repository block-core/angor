using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Portfolio.Manage;

public partial class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;
    private RecoveryState state = RecoveryState.Empty;

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;

        ViewTransaction = ReactiveCommand.Create(() => { }).Enhance();
        
        var loadCommand = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetRecoveryState)).Enhance().DisposeWith(disposables);
        loadCommand.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);
        Load = loadCommand;

        var command = loadCommand.Successes().Select(_ =>  ReactiveCommand.CreateFromTask(async () =>
        {
            // TODO: This will be wired in a follow-up PR
            await uiServices.Dialog.ShowMessage("Action not implemented yet", "");
            return Result.Success();
        }).Enhance());
        Action = command;
    }

    public IObservable<IEnhancedCommand> Action { get; }

    private Task<Result<RecoveryState>> GetRecoveryState(IWallet wallet)
    {
        return investmentAppService
            .GetInvestorProjectRecovery(wallet.Id.Value, projectId)
            .Map(dto => CreateRecoveryViewModel(wallet.Id.Value, dto));
    }
    
    public IAmountUI TotalFunds => Project.TotalFunds;
    public IEnhancedCommand ViewTransaction { get; }
    public DateTime ExpiryDate => Project.ExpiryDate;
    public TimeSpan PenaltyPeriod => Project.PenaltyPeriod;
    public IEnumerable<IInvestorProjectItem> Items => state.Items;
    public IInvestedProject Project => state.Project;
    public IEnhancedCommand Load { get; }

    private static RecoveryState CreateRecoveryViewModel(Guid walletId, InvestorProjectRecoveryDto dto)
    {
        var project = new InvestedProject(dto);
        var items = dto.Items
            .Select(x => (IInvestorProjectItem)new InvestorProjectItem(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                status: x.Status))
            .ToList();
        
        return new RecoveryState(walletId, project, items);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }

    private class InvestorProjectItem : ReactiveObject, IInvestorProjectItem
    {
        public InvestorProjectItem(int stage, IAmountUI amount, string status)
        {
            Stage = stage;
            Amount = amount;
            Status = status;
        }

        public int Stage { get; }
        public IAmountUI Amount { get; }
        public string Status { get; }
    }

    private class InvestedProject : IInvestedProject
    {
        public InvestedProject(InvestorProjectRecoveryDto dto)
        {
            TotalFunds = new AmountUI(dto.TotalSpendable);
            ExpiryDate = dto.ExpiryDate;
            PenaltyPeriod = TimeSpan.FromDays(dto.PenaltyDays);
            Name = dto.Name ?? dto.ProjectIdentifier;
        }

        public IAmountUI TotalFunds { get; }
        public DateTime ExpiryDate { get; }
        public TimeSpan PenaltyPeriod { get; }
        public string Name { get; }
    }

    private sealed record RecoveryState(Guid? WalletId, IInvestedProject Project, IReadOnlyList<IInvestorProjectItem> Items)
    {
        public static RecoveryState Empty { get; } = new(null, new InvestedProjectDesign(), []);
    }
}

using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Extensions;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public partial class ReleaseFundsViewModel : ReactiveObject, IReleaseFundsViewModel, IDisposable
{
    [ObservableAsProperty]
    private IEnumerable<IUnfundedProjectTransaction> transactions;

    private readonly ProjectId projectId;

    private readonly IInvestmentAppService investmentAppService;

    private readonly UIServices uiServices;

    private readonly CompositeDisposable disposable = new();

    public ReleaseFundsViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;
        
        LoadTransactions = WalletCommand.Create(wallet => investmentAppService.GetReleaseableTransactions(wallet.Id.Value, projectId)
            .MapEach(dto => (IUnfundedProjectTransaction)new UnfundedProjectTransaction(wallet.Id.Value, projectId, dto, investmentAppService, uiServices)), uiServices.WalletRoot)
            .DisposeWith(disposable);

        transactionsHelper = LoadTransactions
            .Successes()
            .ToProperty(this, model => model.Transactions)
            .DisposeWith(disposable);

        LoadTransactions.Execute().Subscribe().DisposeWith(disposable);
        ReleaseAll = WalletCommand.Create(DoReleaseAll, uiServices.WalletRoot, this.WhenAnyValue(model => model.Transactions).NotNull())
            .Enhance()
            .DisposeWith(disposable);
    }

    private Task<Maybe<Result>> DoReleaseAll(IWallet wallet)
    {
        var addresses = Transactions.Select(transaction => transaction.InvestorAddress);
        
        return UserFlow.PromptAndNotify(
            () => investmentAppService.ReleaseInvestorTransactions(wallet.Id.Value, projectId, addresses), uiServices, 
            "Are you sure you want to release all the funds?", 
            "Confirm Release All", 
            "Successfully released all",
            "Released All", e => $"Cannot release the funds {e}");
    }

    public ReactiveCommand<Unit, Result<IEnumerable<IUnfundedProjectTransaction>>> LoadTransactions { get; }

    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
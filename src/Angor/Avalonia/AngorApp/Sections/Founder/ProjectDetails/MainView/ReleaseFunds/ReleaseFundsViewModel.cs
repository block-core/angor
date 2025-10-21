using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;
using AngorApp.Core;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public partial class ReleaseFundsViewModel : ReactiveObject, IReleaseFundsViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    private readonly IFullProject project;
    private readonly IFounderAppService founderAppService;
    private readonly UIServices uiServices;

    [ObservableAsProperty] private IEnumerable<IUnfundedProjectTransaction> transactions;

    public ReleaseFundsViewModel(IFullProject project, IFounderAppService founderAppService, IWalletContext walletContext, UIServices uiServices)
    {
        this.project = project;
        this.founderAppService = founderAppService;
        this.uiServices = uiServices;

        RefreshTransactions = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetTransactions), Observable.Return(project.Status == ProjectStatus.Failed))
            .DisposeWith(disposable);

        transactionsHelper = RefreshTransactions
            .Successes()
            .ToProperty(this, model => model.Transactions)
            .DisposeWith(disposable);

        ReleaseAll = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(wallet => DoReleaseAll(wallet)).ToMaybeResult(), this.WhenAnyValue(model => model.Transactions).NotNull())
            .Enhance()
            .DisposeWith(disposable);
        
        RefreshWhenAnyCommandExecutes().DisposeWith(disposable);
        RefreshTransactions.Execute().Subscribe().DisposeWith(disposable);
    }

    public ReactiveCommand<Unit, Result<List<IUnfundedProjectTransaction>>> RefreshTransactions { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }

    private Task<Result<List<IUnfundedProjectTransaction>>> GetTransactions(IWallet wallet)
    {
        return founderAppService.GetReleasableTransactions(wallet.Id.Value, project.ProjectId)
            .MapEach(IUnfundedProjectTransaction (dto) => new UnfundedProjectTransaction(wallet.Id.Value, project.ProjectId, dto, founderAppService, uiServices))
            .Map(enumerable => enumerable.ToList());
    }

    private IDisposable RefreshWhenAnyCommandExecutes()
    {
        return new[]
        {
            OnReleaseAllExecuted(),
            OnChildReleaseCommandExecuted()
        }.Merge().InvokeCommand(RefreshTransactions);
    }

    private IObservable<Unit> OnReleaseAllExecuted()
    {
        return ReleaseAll.Values().ToSignal();
    }

    private IObservable<Unit> OnChildReleaseCommandExecuted()
    {
        return this.WhenAnyValue(model => model.Transactions)
            .WhereNotNull()
            .Select(enumerable => enumerable.Select(transaction => transaction.Release.Values()).Merge().ToSignal())
            .Switch();
    }

    private Task<Maybe<Result>> DoReleaseAll(IWallet wallet)
    {
        var addresses = Transactions.Select(transaction => transaction.InvestmentEventId);
        
        return UserFlow.PromptAndNotify(
            () => founderAppService.ReleaseInvestorTransactions(wallet.Id.Value, project.ProjectId, addresses), uiServices,
            "Are you sure you want to release all the funds?",
            "Confirm Release All",
            "Successfully released all",
            "Released All", e => $"Cannot release the funds {e}");
    }
}

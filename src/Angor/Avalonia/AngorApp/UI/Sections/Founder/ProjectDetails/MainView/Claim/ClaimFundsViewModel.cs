using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Shared.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;

public partial class ClaimFundsViewModel : ReactiveObject, IClaimFundsViewModel, IDisposable
{
    private readonly IFounderAppService founderAppService;
    private readonly ProjectId projectId;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;
    [ObservableAsProperty] private IEnumerable<IClaimableStage>? claimableStages;

    private readonly CompositeDisposable disposable = new();

    public ClaimFundsViewModel(ProjectId projectId, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.founderAppService = founderAppService;
        this.projectId = projectId;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        LoadClaimableStages = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetClaimableStages))
            .Enhance()
            .DisposeWith(disposable);
        
        LoadClaimableStages.HandleErrorsWith(uiServices.NotificationService, "Error loading claimable stages")
            .DisposeWith(disposable);
        
        claimableStagesHelper = LoadClaimableStages.Successes().ToProperty(this, x => x.ClaimableStages).DisposeWith(disposable);

        LoadClaimableStages.Successes()
            .Select(stages => stages.Select(stage => stage.Claim.Values()).Merge())
            .Switch()
            .SelectMany(_ => LoadClaimableStages.Execute())
            .Subscribe()
            .DisposeWith(disposable);
        claimableStagesHelper.DisposeWith(disposable);
    }

    private Task<Result<IEnumerable<IClaimableStage>>> GetClaimableStages(IWallet wallet)
    {
        return founderAppService
            .GetClaimableTransactions(wallet.Id, projectId)
            .Map(response => CreateStage(response.Transactions));
    }

    private IEnumerable<IClaimableStage> CreateStage(IEnumerable<ClaimableTransactionDto> claimableTransactionDto)
    {
        return claimableTransactionDto.GroupBy(dto => dto.StageNumber)
            .Select(IClaimableStage (group) =>
            {
                var claimableTransactions = group.Select(IClaimableTransaction (dto) => new ClaimableTransaction(dto)).ToList();
                return new ClaimableStage(projectId, group.Key, claimableTransactions.ToList(), founderAppService, uiServices, walletContext);
            })
            .ToList();
    }

    public IEnhancedCommand<Result<IEnumerable<IClaimableStage>>> LoadClaimableStages { get; }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}

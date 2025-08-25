using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Extensions;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public partial class ClaimFundsViewModel : ReactiveObject, IClaimFundsViewModel, IDisposable
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly ProjectId projectId;
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IEnumerable<IClaimableStage>? claimableStages;

    private readonly CompositeDisposable disposable = new();
    public ClaimFundsViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.investmentAppService = investmentAppService;
        this.projectId = projectId;
        this.uiServices = uiServices;
        LoadClaimableStages = WalletCommand.Create(GetClaimableStages, uiServices.WalletRoot)
            .Enhance()
            .DisposeWith(disposable);
        
        LoadClaimableStages.HandleErrorsWith(uiServices.NotificationService, "Error loading claimable stages")
            .DisposeWith(disposable);
        
        claimableStagesHelper = LoadClaimableStages.Successes().ToProperty(this, x => x.ClaimableStages).DisposeWith(disposable);

        // Reload the claimable stages after any Claim command executes in the latest set of stages
        LoadClaimableStages.Successes()
            .Select(stages => stages.Select(stage => stage.Claim.Values()).Merge())
            .Switch()
            .SelectMany(_ => LoadClaimableStages.Execute())
            .Subscribe()
            .DisposeWith(disposable);
        
        LoadClaimableStages.Execute().Subscribe().DisposeWith(disposable);
        claimableStagesHelper.DisposeWith(disposable);
    }

    private Task<Result<IEnumerable<IClaimableStage>>> GetClaimableStages(IWallet wallet)
    {
        return investmentAppService
            .GetClaimableTransactions(wallet.Id.Value, projectId)
            .Map(CreateStage);
    }

    private IEnumerable<IClaimableStage> CreateStage(IEnumerable<ClaimableTransactionDto> claimableTransactionDto)
    {
        var stages = claimableTransactionDto.GroupBy(dto => dto.StageId)
            .Select(IClaimableStage (group) =>
            {
                var claimableTransactions = group.Select(IClaimableTransaction (dto) => new ClaimableTransaction(dto)).ToList();
                return new ClaimableStage(projectId, group.Key, claimableTransactions.ToList(), investmentAppService, uiServices);
            }).ToList();
        return stages;
    }

    public IEnhancedCommand<Result<IEnumerable<IClaimableStage>>> LoadClaimableStages { get; }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Extensions;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public partial class StageClaimViewModel : ReactiveObject, IStageClaimViewModel, IDisposable
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly ProjectId projectId;
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IEnumerable<IClaimableStage> claimableStages;

    private readonly CompositeDisposable disposable = new();
    public StageClaimViewModel(IInvestmentAppService investmentAppService, ProjectId projectId, UIServices uiServices)
    {
        this.investmentAppService = investmentAppService;
        this.projectId = projectId;
        this.uiServices = uiServices;
        Load = WalletCommand.Create(wallet => GetClaimableStages(wallet), uiServices.WalletRoot)
            .Enhance()
            .DisposeWith(disposable);
        
        Load.HandleErrorsWith(uiServices.NotificationService, "Error loading claimable stages")
            .DisposeWith(disposable);
            
        claimableStagesHelper = Load.Successes().ToProperty(this, x => x.ClaimableStages).DisposeWith(disposable);

        Load.Execute().Subscribe().DisposeWith(disposable);
        claimableStagesHelper.DisposeWith(disposable);
    }

    private Task<Result<IEnumerable<IClaimableStage>>> GetClaimableStages(IWallet wallet)
    {
        return investmentAppService
            .GetClaimableTransactions(wallet.Id.Value, projectId)
            .Map(claimableTransactionDto =>
            {
                var groupedByStage = claimableTransactionDto.GroupBy(dto => dto.StageId)
                    .Select(IClaimableStage (group) =>
                    {
                        var claimableTransactions = group.Select(IClaimableTransaction (dto) => new ClaimableTransaction(dto)).ToList();
                        return new ClaimableStage(projectId, group.Key, claimableTransactions.ToList(), investmentAppService, uiServices);
                    });
                return groupedByStage;
            });
    }

    public IEnhancedCommand<Result<IEnumerable<IClaimableStage>>> Load { get; set; }
    
    public DateTime EstimatedCompletion { get; set; } = DateTime.Now.AddDays(30);

    public void Dispose()
    {
        disposable.Dispose();
    }
}
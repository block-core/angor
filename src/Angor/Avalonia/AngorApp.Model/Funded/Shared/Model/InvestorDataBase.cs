using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.Shared.Services;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Model.Funded.Shared.Model;

public abstract class InvestorDataBase : IInvestorData, IDisposable
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly IWalletContext walletContext;
    private readonly CompositeDisposable disposables = new();
    private readonly BehaviorSubject<InvestmentStatus> status;
    private readonly BehaviorSubject<RecoveryState> recovery = new(RecoveryState.None);
    private readonly BehaviorSubject<IReadOnlyList<InvestorStageItemDto>> stageItems = new(new List<InvestorStageItemDto>());

    protected InvestorDataBase(InvestedProjectDto dto, IInvestmentAppService investmentAppService, IWalletContext walletContext)
    {
        this.investmentAppService = investmentAppService;
        this.walletContext = walletContext;

        InvestmentId = dto.InvestmentId;
        ProjectId = dto.Id;

        Amount = new AmountUI(dto.Investment.Sats);
        InvestedOn = dto.RequestedOn ?? DateTimeOffset.MinValue;
        status = new BehaviorSubject<InvestmentStatus>(dto.InvestmentStatus);
        Status = status;
        Recovery = recovery;
        StageItems = stageItems;

        var refresh = EnhancedCommand.CreateWithResult(DoRefresh).DisposeWith(disposables);
        refresh.Successes()
            .Subscribe(Update)
            .DisposeWith(disposables);
        Refresh = refresh;
    }

    public IAmountUI Amount { get; }
    public DateTimeOffset InvestedOn { get; private set; }
    public IEnhancedCommand Refresh { get; }
    public string InvestmentId { get; }
    public string ProjectId { get; }
    public IObservable<InvestmentStatus> Status { get; }
    public IObservable<RecoveryState> Recovery { get; }
    public IObservable<IReadOnlyList<InvestorStageItemDto>> StageItems { get; }

    private async Task<Result<(InvestedProjectDto Dto, RecoveryState Recovery, IReadOnlyList<InvestorStageItemDto> Items)>> DoRefresh()
    {
        return await walletContext
            .Require()
            .Bind(async wallet =>
            {
                var dto = await investmentAppService
                    .GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id))
                    .Bind(response => response.Projects
                        .TryFirst(project => project.Id == ProjectId)
                        .ToResult($"Investment not found: {ProjectId}"));

                if (dto.IsFailure)
                    return Result.Failure<(InvestedProjectDto, RecoveryState, IReadOnlyList<InvestorStageItemDto>)>(dto.Error);

                var recoveryState = dto.Value.InvestmentStatus == InvestmentStatus.Invested
                    ? recovery.Value
                    : RecoveryState.None;

                IReadOnlyList<InvestorStageItemDto> recoveryItems = new List<InvestorStageItemDto>();

                if (dto.Value.InvestmentStatus == InvestmentStatus.Invested)
                {
                    var recoveryResult = await investmentAppService.GetRecoveryStatus(
                        new GetRecoveryStatus.GetRecoveryStatusRequest(wallet.Id, new ProjectId(ProjectId)));

                    if (recoveryResult.IsSuccess)
                    {
                        var r = recoveryResult.Value.RecoveryData;
                        recoveryState = new RecoveryState(r.HasUnspentItems, r.HasItemsInPenalty, r.HasReleaseSignatures, r.EndOfProject, r.IsAboveThreshold);
                        recoveryItems = r.Items;
                    }
                }

                return Result.Success((dto.Value, recoveryState, recoveryItems));
            });
    }

    private void Update((InvestedProjectDto Dto, RecoveryState Recovery, IReadOnlyList<InvestorStageItemDto> Items) result)
    {
        InvestedOn = result.Dto.RequestedOn ?? DateTimeOffset.MinValue;
        status.OnNext(result.Dto.InvestmentStatus);
        recovery.OnNext(result.Recovery);
        stageItems.OnNext(result.Items);
    }

    public void Dispose()
    {
        disposables.Dispose();
        status.Dispose();
        recovery.Dispose();
        stageItems.Dispose();
    }
}

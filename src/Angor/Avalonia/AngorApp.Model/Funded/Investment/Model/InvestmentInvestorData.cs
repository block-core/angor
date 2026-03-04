using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.Shared.Services;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Model.Funded.Investment.Model
{
    public sealed class InvestmentInvestorData : IInvestmentInvestorData, IDisposable
    {
        private readonly IInvestmentAppService investmentAppService;
        private readonly IWalletContext walletContext;
        private readonly CompositeDisposable disposables = new();
        private readonly BehaviorSubject<InvestmentStatus> status;
        private readonly BehaviorSubject<RecoveryState> recovery = new(RecoveryState.None);

        public InvestmentInvestorData(InvestedProjectDto dto, IInvestmentAppService investmentAppService, IWalletContext walletContext)
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

        private async Task<Result<(InvestedProjectDto Dto, RecoveryState Recovery)>> DoRefresh()
        {
            return await walletContext
                .Require()
                .Bind(async wallet =>
                {
                    var dto = await investmentAppService
                        .GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id))
                        .Bind(response => response.Projects
                            .TryFirst(project => project.Id == ProjectId && (string.IsNullOrEmpty(InvestmentId) || project.InvestmentId == InvestmentId))
                            .ToResult($"Investment not found: {ProjectId}"));

                    if (dto.IsFailure)
                        return Result.Failure<(InvestedProjectDto, RecoveryState)>(dto.Error);

                    var recoveryState = RecoveryState.None;

                    if (dto.Value.InvestmentStatus == InvestmentStatus.Invested)
                    {
                        var recoveryResult = await investmentAppService.GetRecoveryStatus(
                            new GetRecoveryStatus.GetRecoveryStatusRequest(wallet.Id, new ProjectId(ProjectId), InvestmentId));

                        if (recoveryResult.IsSuccess)
                        {
                            var r = recoveryResult.Value.RecoveryData;
                            recoveryState = new RecoveryState(r.HasUnspentItems, r.HasItemsInPenalty, r.HasReleaseSignatures, r.EndOfProject, r.IsAboveThreshold);
                        }
                    }

                    return Result.Success((dto.Value, recoveryState));
                });
        }

        private void Update((InvestedProjectDto Dto, RecoveryState Recovery) result)
        {
            InvestedOn = result.Dto.RequestedOn ?? DateTimeOffset.MinValue;
            status.OnNext(result.Dto.InvestmentStatus);
            recovery.OnNext(result.Recovery);
        }

        public void Dispose()
        {
            disposables.Dispose();
            status.Dispose();
            recovery.Dispose();
        }
    }
}

using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    public sealed class InvestmentInvestorData : IInvestmentInvestorData, IDisposable
    {
        private readonly IInvestmentAppService investmentAppService;
        private readonly IWalletContext walletContext;
        private readonly CompositeDisposable disposables = new();
        private readonly BehaviorSubject<InvestmentStatus> status;

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

            var refresh = EnhancedCommand.CreateWithResult(DoRefresh).DisposeWith(disposables);
            refresh.Successes()
                   .Subscribe(UpdateFromDto)
                   .DisposeWith(disposables);
            Refresh = refresh;
        }

        public IAmountUI Amount { get; }
        public DateTimeOffset InvestedOn { get; }
        public IEnhancedCommand Refresh { get; }
        public string InvestmentId { get; }
        public string ProjectId { get; }
        public IObservable<InvestmentStatus> Status { get; }

        private async Task<Result<InvestedProjectDto>> DoRefresh()
        {
            return await walletContext
                         .Require()
                         .Bind(wallet => investmentAppService.GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id)))
                         .Bind(response => response.Projects
                                                  .TryFirst(project => project.Id == ProjectId)
                                                  .ToResult($"Investment not found: {ProjectId}"));
        }

        private void UpdateFromDto(InvestedProjectDto dto)
        {
            status.OnNext(dto.InvestmentStatus);
        }

        public void Dispose()
        {
            disposables.Dispose();
            status.Dispose();
        }
    }
}

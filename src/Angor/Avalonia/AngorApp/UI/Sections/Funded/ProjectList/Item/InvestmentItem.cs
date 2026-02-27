using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.UI.Shell;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item
{
    public class InvestmentItem : IInvestmentItem, IDisposable
    {
        private readonly IInvestmentAppService investmentAppService;
        private readonly IWalletContext walletContext;
        private readonly CompositeDisposable disposable = new();
        private readonly BehaviorSubject<IAmountUI> amount;
        private readonly BehaviorSubject<InvestmentStatus> status;

        public InvestmentItem(InvestedProjectDto dto, IInvestmentAppService investmentAppService, IWalletContext walletContext)
        {
            this.investmentAppService = investmentAppService;
            this.walletContext = walletContext;

            InvestmentId = dto.InvestmentId;
            amount = new BehaviorSubject<IAmountUI>(new AmountUI(dto.Investment.Sats));
            status = new BehaviorSubject<InvestmentStatus>(dto.InvestmentStatus);
            Amount = amount;
            Date = dto.RequestedOn ?? DateTimeOffset.MinValue;
            Status = status;

            Refresh = EnhancedCommand.CreateWithResult(DoRefresh).DisposeWith(disposable);
            Refresh.Successes()
                .Subscribe(UpdateFromDto)
                .DisposeWith(disposable);
        }

        public string InvestmentId { get; }
        public IObservable<IAmountUI> Amount { get; }
        public DateTimeOffset Date { get; }
        public IObservable<InvestmentStatus> Status { get; }
        public IEnhancedCommand<Result<InvestedProjectDto>> Refresh { get; }

        private async Task<Result<InvestedProjectDto>> DoRefresh()
        {
            return await walletContext.Require()
                .Bind(wallet => investmentAppService.GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id)))
                .Bind(response => response.Projects.TryFirst(project => project.InvestmentId == InvestmentId)
                    .ToResult($"Investment not found: {InvestmentId}"));
        }

        private void UpdateFromDto(InvestedProjectDto dto)
        {
            amount.OnNext(new AmountUI(dto.Investment.Sats));
            status.OnNext(dto.InvestmentStatus);
        }

        public void Dispose()
        {
            disposable.Dispose();
            amount.Dispose();
            status.Dispose();
        }
    }
}

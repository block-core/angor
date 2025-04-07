using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IStep, IDraftViewModel
{
    public DraftViewModel(IInvestmentAppService investmentAppService, WalletId walletId, long sats, IProject project)
    {
        SatsToInvest = sats;
        CreateDraft = ReactiveCommand.CreateFromTask(() => investmentAppService.CreateInvestmentTransaction(walletId.Id, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats)));
        CreateDraft.Subscribe(result => { });
    }

    public ReactiveCommand<Unit,Result<InvestmentTransaction>> CreateDraft { get; set; }

    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public long SatsToInvest { get; }

    [Reactive] private long feerate;
}
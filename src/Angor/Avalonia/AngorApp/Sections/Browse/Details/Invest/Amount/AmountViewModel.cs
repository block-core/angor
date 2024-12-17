using System.Reactive.Linq;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private decimal? amount;

    public AmountViewModel(IWallet wallet, Project project, UIServices uiServices)
    {
        Project = project;
        this.ValidationRule(x => x.Amount, x => x > 0, "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, "Please, specify an amount");
        this.ValidationRule(x => x.Amount, x => x <= wallet.Balance, "The amount should be greater than the wallet balance");
    }

    public Project Project { get; }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}
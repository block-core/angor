using System.Reactive.Linq;
using AngorApp.Model;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private ulong? amount;

    public AmountViewModel(IWallet wallet, IProject project)
    {
        Project = project;
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is not null, _ => "Please, specify an amount");
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is null || x <= wallet.Balance, _ => "Not enough balance");
    }

    public IProject Project { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}
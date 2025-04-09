using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Features.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private long? amount;

    public AmountViewModel(IWallet wallet, IProject project)
    {
        Project = project;

        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        
        var isValidAmount = this
            .WhenAnyValue(x => x.Amount)
            .WithLatestFrom(wallet.Balance, (a, b) => a is null || a <= b);
        
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");
    }

    public Maybe<string> Title => $"Invest in {Project.Name}";
    public IProject Project { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public bool AutoAdvance => false;
    [ObservableAsProperty] private long walletBalance;
    public IObservable<bool> IsBusy => Observable.Return(false);
}
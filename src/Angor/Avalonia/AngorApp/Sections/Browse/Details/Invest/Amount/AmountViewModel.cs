using System.Reactive.Linq;
using Angor.UI.Model;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Browse.Details.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel
{
    [Reactive] private long? amount;

    public AmountViewModel(IWallet wallet, IProject project)
    {
        walletBalanceHelper = wallet.Balance.ToProperty(this, model => model.WalletBalance);
        
        Project = project;
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is not null, _ => "Please, specify an amount");
        var isValidAmount = this.WhenAnyValue(x => x.Amount, x => x.WalletBalance, (a, b) => a is null || a <= b);
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");
    }

    public IProject Project { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    [ObservableAsProperty] private long walletBalance;
}
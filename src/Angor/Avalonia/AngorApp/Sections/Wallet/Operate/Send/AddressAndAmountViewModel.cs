using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.Operate.Send;

public partial class AddressAndAmountViewModel : ReactiveValidationObject, IAddressAndAmountViewModel
{
    [Reactive] private string? address;
    [Reactive] private long? amount;

    public AddressAndAmountViewModel(IWallet wallet)
    {
        walletBalanceHelper = wallet.Balance.ToProperty(this, model => model.WalletBalance);
        
        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        var isValidAmount = this.WhenAnyValue(x => x.Amount, x => x.WalletBalance, (a, b) => a is null || a <= b);
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");

        this.ValidationRule<AddressAndAmountViewModel, string>(x => x.Address, x => !string.IsNullOrWhiteSpace(x), _ => "Please, specify an address");
        this.ValidationRule<AddressAndAmountViewModel, string>(x => x.Address, x => string.IsNullOrWhiteSpace(x) || wallet.IsAddressValid(x).IsSuccess, message => wallet.IsAddressValid(message).Error);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    [ObservableAsProperty] private long walletBalance;
}
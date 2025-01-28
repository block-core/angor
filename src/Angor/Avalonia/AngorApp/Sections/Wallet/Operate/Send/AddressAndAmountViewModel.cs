using System.Reactive.Linq;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.Operate.Send;

public partial class AddressAndAmountViewModel : ReactiveValidationObject, IAddressAndAmountViewModel
{
    [Reactive] private string? address = SampleData.TestNetBitcoinAddress;
    [Reactive] private long? amount;

    public AddressAndAmountViewModel(IWallet wallet)
    {
        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        this.ValidationRule(x => x.Amount, this.WhenAnyValue(x => x.Amount), x => x is null || x <= wallet.Balance, _ => "Not enough balance");

        this.ValidationRule<AddressAndAmountViewModel, string>(x => x.Address, x => !string.IsNullOrWhiteSpace(x), _ => "Please, specify an address");
        this.ValidationRule<AddressAndAmountViewModel, string>(x => x.Address, x => string.IsNullOrWhiteSpace(x) || wallet.IsAddressValid(x).IsSuccess, message => wallet.IsAddressValid(message).Error);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}
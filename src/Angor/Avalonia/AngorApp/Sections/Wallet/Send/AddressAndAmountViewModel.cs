using System.Reactive.Linq;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Send;

public partial class AddressAndAmountViewModel : ReactiveValidationObject, IAddressAndAmountViewModel
{
    [Reactive] private string? address;
    [Reactive] private decimal? amount;
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    public AddressAndAmountViewModel(IWallet wallet)
    {
        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ =>  "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        this.ValidationRule(x => x.Amount, x => x is null || x <= wallet.Balance, _ => "The amount should be greater than the wallet balance");
        
        this.ValidationRule(x => x.Address, x => !string.IsNullOrWhiteSpace(x), _ => "Please, specify an address");
        this.ValidationRule(x => x.Address, x => x is null || wallet.IsAddressValid(x).IsSuccess, message => wallet.IsAddressValid(message).Error);
    }
}

public interface IAddressAndAmountViewModel: IStep
{
    public decimal? Amount { get; set; }
    public string? Address { get; set; }
}
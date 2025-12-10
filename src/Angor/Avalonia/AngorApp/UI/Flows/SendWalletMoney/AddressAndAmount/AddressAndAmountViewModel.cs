using Branta.V2.Classes;
using Branta.V2.Models;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount;

public partial class AddressAndAmountViewModel : ReactiveValidationObject, IAddressAndAmountViewModel, IValidatable
{
    [Reactive] private string? address;
    [Reactive] private long? amount;
    [Reactive] private bool brantaLoading = false;
    [Reactive] public bool showBrantaVerification = false;
    [Reactive] public bool brantaPaymentsFound = false;
    [Reactive] public List<Payment>? brantaPayments = [];
    [ObservableAsProperty] private IAmountUI? walletBalance;

    private readonly BrantaClient _brantaClient;


    public AddressAndAmountViewModel(IWallet wallet, BrantaClient brantaClient)
    {
        _brantaClient = brantaClient;

        walletBalanceHelper = wallet.WhenAnyValue(w => w.Balance)
            .ToProperty(this, model => model.WalletBalance);

        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        var isValidAmount = this.WhenAnyValue(x => x.Amount, x => x.WalletBalance, (amount, balance) => amount is null || amount <= balance.Sats);
        this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");

        this.ValidationRule(x => x.Address, x => !string.IsNullOrWhiteSpace(x), _ => "Please, specify an address");
        this.ValidationRule(x => x.Address, x => string.IsNullOrWhiteSpace(x) || wallet.IsAddressValid(x).IsSuccess, message => wallet.IsAddressValid(message).Error);

        this.WhenAnyValue(x => x.Address)
            .Subscribe(async newAddress => {
                await OnAddressChangedAsync(newAddress);
            });
    }

    public IObservable<bool> IsValid => this.IsValid();

    private async Task OnAddressChangedAsync(string? newAddress)
    {
        BrantaLoading = true;

        if (string.IsNullOrEmpty(newAddress)) {
            BrantaPayments = null;
            ShowBrantaVerification = false;
            BrantaLoading = false;
            return;
        }

        var payments = await _brantaClient.GetPaymentsAsync(newAddress);

        BrantaPayments = payments;
        BrantaPaymentsFound = payments.Count != 0;
        ShowBrantaVerification = true;

        BrantaLoading = false;
    }
}

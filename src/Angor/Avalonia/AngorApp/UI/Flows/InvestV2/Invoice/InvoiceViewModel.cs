using System.Linq;

using AngorApp.UI.Flows.Invest.Amount;

namespace AngorApp.UI.Flows.InvestV2.Invoice;

public partial class InvoiceViewModel : ReactiveObject, IInvoiceViewModel, IValidatable
{
    public InvoiceViewModel()
    {
        SelectedInvoiceType = InvoiceTypes.First();
        PaymentReceived = Observable.Timer(TimeSpan.FromSeconds(3), RxApp.MainThreadScheduler).Select(l => true).StartWith(false);
    }

    public IObservable<bool> PaymentReceived { get; }

    public IAmountUI Amount { get; } = AmountUI.FromBtc(0.45m);
    public IEnumerable<IInvoiceType> InvoiceTypes { get; } =
    [
        new InvoiceTypeSample() { Name = "Bitcoin Address", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh", },
    ];

    [Reactive] private IInvoiceType? selectedInvoiceType;
    public IObservable<bool> IsValid => PaymentReceived;
}
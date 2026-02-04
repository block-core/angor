using System.Linq;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.InvestV2.Invoice;

public partial class InvoiceViewModelSample : ReactiveObject, IInvoiceViewModel
{
    public InvoiceViewModelSample()
    {
        SelectedInvoiceType = InvoiceTypes.First();
        CopyAddress = EnhancedCommand.Create(() => { });
    }
        
    public IAmountUI Amount { get; } = AmountUI.FromBtc(0.5m);
    public IObservable<bool> PaymentReceived { get; } = Observable.Return(true);

    public IEnumerable<IInvoiceType> InvoiceTypes { get; } =
    [
        new InvoiceTypeSample() { Name = "Lightning Invoice", Address = "lnbc1pvjluezsp5un3qexampleinvoice0s28uz", },
        new InvoiceTypeSample() { Name = "Bitcoin Address", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh", },
        new InvoiceTypeSample() { Name = "Liquid Address", Address = "ex1qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq", }
    ];

    [Reactive] private IInvoiceType? selectedInvoiceType;
    
    public IEnhancedCommand CopyAddress { get; }
    
    public void SetCloseable(ICloseable closeable)
    {
        // No-op for sample
    }
}
using System.Linq;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.InvestV2.Invoice;

public partial class InvoiceViewModelSample : ReactiveObject, IInvoiceViewModel
{
    public InvoiceViewModelSample()
    {
        InvoiceTypes =
        [
            new OnChainInvoiceType { Name = "Bitcoin Address", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" },
            CreateSampleLightningInvoice(),
            CreateSampleLiquidInvoice()
        ];
        SelectedInvoiceType = InvoiceTypes.First();
        CopyAddress = EnhancedCommand.Create(() => { });
    }
        
    public IAmountUI Amount { get; } = AmountUI.FromBtc(0.5m);
    public IObservable<bool> PaymentReceived { get; } = Observable.Return(true);

    public IEnumerable<IInvoiceType> InvoiceTypes { get; }

    private static LightningInvoiceType CreateSampleLightningInvoice()
    {
        var invoice = new LightningInvoiceType
        {
            Name = "Lightning",
            ReceivingAddress = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"
        };
        invoice.Address = "lnbc1pvjluezsp5un3qexampleinvoice0s28uz";
        invoice.SwapId = "sample-swap-id";
        return invoice;
    }

    private static LiquidInvoiceType CreateSampleLiquidInvoice()
    {
        var invoice = new LiquidInvoiceType
        {
            Name = "Liquid",
            ReceivingAddress = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"
        };
        invoice.Address = "tlq1qqfexample0liquidaddress0here";
        invoice.SwapId = "sample-liquid-swap-id";
        invoice.ExpectedLiquidAmount = 100000;
        return invoice;
    }

    [Reactive] private IInvoiceType? selectedInvoiceType;
    
    public bool IsLoadingInvoice => false;
    
    public IEnhancedCommand CopyAddress { get; }
    
    public void SetCloseable(ICloseable closeable)
    {
        // No-op for sample
    }
}
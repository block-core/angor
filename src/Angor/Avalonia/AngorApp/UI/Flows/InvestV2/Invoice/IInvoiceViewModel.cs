namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    public interface IInvoiceViewModel
    {
        IEnumerable<IInvoiceType> InvoiceTypes { get; }
        IInvoiceType? SelectedInvoiceType { get; set; }
        IAmountUI Amount { get; }
        IObservable<bool> PaymentReceived { get; }
        IEnhancedCommand CopyAddress { get; }
    }
}
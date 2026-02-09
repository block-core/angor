namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    /// <summary>
    /// Design-time sample for IInvoiceType. Defaults to OnChain for simplicity.
    /// </summary>
    public class InvoiceTypeSample : IInvoiceType
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.OnChain;
        public string? SwapId { get; set; }
        public string? ReceivingAddress { get; set; }
    }
}
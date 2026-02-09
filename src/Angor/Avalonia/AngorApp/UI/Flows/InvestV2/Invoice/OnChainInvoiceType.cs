namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    /// <summary>
    /// Invoice type for on-chain Bitcoin payments.
    /// </summary>
    public class OnChainInvoiceType : IInvoiceType
    {
        public required string Name { get; init; }
        public required string Address { get; set; }
        public PaymentMethod PaymentMethod => PaymentMethod.OnChain;
        public string? SwapId => null;
        public string? ReceivingAddress => Address;
    }
}

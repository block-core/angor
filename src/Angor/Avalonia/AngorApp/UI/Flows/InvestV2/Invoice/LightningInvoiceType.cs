namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    /// <summary>
    /// Invoice type for Lightning payments via Boltz submarine swap.
    /// Address contains the Lightning invoice to pay.
    /// ReceivingAddress is where funds will arrive on-chain after swap completes.
    /// </summary>
    public class LightningInvoiceType : IInvoiceType
    {
        public required string Name { get; init; }
        
        /// <summary>
        /// The Lightning invoice (BOLT11) to display/pay.
        /// Empty string when not yet loaded.
        /// </summary>
        public required string Address { get; set; }
        
        public PaymentMethod PaymentMethod => PaymentMethod.Lightning;
        
        /// <summary>
        /// The Boltz swap ID for monitoring swap status.
        /// Null when invoice is not yet loaded.
        /// </summary>
        public string? SwapId { get; set; }
        
        /// <summary>
        /// The on-chain address where Boltz will deposit funds after Lightning payment.
        /// </summary>
        public required string ReceivingAddress { get; set; }
        
        /// <summary>
        /// Indicates whether the Lightning invoice has been loaded from Boltz.
        /// </summary>
        public bool IsLoaded => !string.IsNullOrEmpty(SwapId) && !string.IsNullOrEmpty(Address);
    }
}

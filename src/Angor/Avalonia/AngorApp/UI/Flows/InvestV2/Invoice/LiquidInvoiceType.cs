namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    /// <summary>
    /// Invoice type for Liquid payments via Boltz submarine swap.
    /// Address contains the Liquid BIP21 URI or address to pay.
    /// ReceivingAddress is where funds will arrive on-chain (BTC) after swap completes.
    /// </summary>
    public partial class LiquidInvoiceType : ReactiveObject, IInvoiceType
    {
        public required string Name { get; init; }
        
        /// <summary>
        /// The Liquid address or BIP21 URI to display/pay.
        /// Empty string when not yet loaded.
        /// </summary>
        [Reactive] private string address = "";
        
        public PaymentMethod PaymentMethod => PaymentMethod.Liquid;
        
        /// <summary>
        /// The Boltz swap ID for monitoring swap status.
        /// Null when invoice is not yet loaded.
        /// </summary>
        [Reactive] private string? swapId;
        
        /// <summary>
        /// The on-chain BTC address where Boltz will deposit funds after Liquid payment.
        /// </summary>
        public required string ReceivingAddress { get; set; }
        
        /// <summary>
        /// The expected amount in satoshis on the Liquid side.
        /// </summary>
        [Reactive] private long expectedLiquidAmount;
        
        /// <summary>
        /// Indicates whether the Liquid invoice has been loaded from Boltz.
        /// </summary>
        public bool IsLoaded => !string.IsNullOrEmpty(SwapId) && !string.IsNullOrEmpty(Address);
    }
}


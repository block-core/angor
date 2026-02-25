namespace AngorApp.UI.Flows.InvestV2.Invoice
{
    public enum PaymentMethod
    {
        OnChain,
        Lightning,
        Liquid
    }

    public interface IInvoiceType
    {
        public string Name { get; }
        public string Address { get; set; }
        public PaymentMethod PaymentMethod { get; }
        
        /// <summary>
        /// Boltz swap ID for Lightning payments. Null for on-chain.
        /// </summary>
        public string? SwapId { get; }
        
        /// <summary>
        /// On-chain receiving address where funds will arrive.
        /// For Lightning, this is where Boltz deposits after swap completes.
        /// </summary>
        public string? ReceivingAddress { get; }
    }
}
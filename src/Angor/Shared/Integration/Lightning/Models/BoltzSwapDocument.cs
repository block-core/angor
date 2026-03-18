namespace Angor.Shared.Integration.Lightning.Models
{
    /// <summary>
    /// Document for storing Boltz swap data in the database.
    /// </summary>
    public class BoltzSwapDocument
    {
        /// <summary>The swap ID (used as document ID)</summary>
        public string SwapId { get; set; } = string.Empty;

        /// <summary>The wallet ID this swap belongs to</summary>
        public string WalletId { get; set; } = string.Empty;

        /// <summary>The project ID (if this swap is for an investment)</summary>
        public string? ProjectId { get; set; }

        /// <summary>The Lightning invoice to pay</summary>
        public string Invoice { get; set; } = string.Empty;

        /// <summary>The destination on-chain address</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>The Boltz lockup address (where funds are held before claiming)</summary>
        public string LockupAddress { get; set; } = string.Empty;

        /// <summary>Expected amount in satoshis</summary>
        public long ExpectedAmount { get; set; }

        /// <summary>Invoice amount in satoshis</summary>
        public long InvoiceAmount { get; set; }

        /// <summary>Timeout block height for the swap</summary>
        public long TimeoutBlockHeight { get; set; }

        /// <summary>The swap tree (serialized JSON) containing claim and refund scripts</summary>
        public string SwapTree { get; set; } = string.Empty;

        /// <summary>Boltz's refund public key</summary>
        public string RefundPublicKey { get; set; } = string.Empty;

        /// <summary>Our claim public key</summary>
        public string ClaimPublicKey { get; set; } = string.Empty;

        /// <summary>The preimage (secret) for claiming - IMPORTANT: store securely!</summary>
        public string Preimage { get; set; } = string.Empty;

        /// <summary>SHA256 hash of the preimage</summary>
        public string PreimageHash { get; set; } = string.Empty;

        /// <summary>Current status of the swap</summary>
        public string Status { get; set; } = "created";

        /// <summary>The lockup transaction ID (once available)</summary>
        public string? LockupTransactionId { get; set; }

        /// <summary>The lockup transaction hex (for claiming)</summary>
        public string? LockupTransactionHex { get; set; }

        /// <summary>The claim transaction ID (once claimed)</summary>
        public string? ClaimTransactionId { get; set; }

        /// <summary>When the swap was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the swap was last updated</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Convert to BoltzSubmarineSwap model</summary>
        public BoltzSubmarineSwap ToSwapModel()
        {
            return new BoltzSubmarineSwap
            {
                Id = SwapId,
                Invoice = Invoice,
                Address = Address,
                LockupAddress = LockupAddress,
                ExpectedAmount = ExpectedAmount,
                InvoiceAmount = InvoiceAmount,
                TimeoutBlockHeight = TimeoutBlockHeight,
                SwapTree = SwapTree,
                RefundPublicKey = RefundPublicKey,
                ClaimPublicKey = ClaimPublicKey,
                Preimage = Preimage,
                PreimageHash = PreimageHash,
                Status = Enum.TryParse<SwapState>(Status, true, out var state) ? state : SwapState.Created
            };
        }

        /// <summary>Create from BoltzSubmarineSwap model</summary>
        public static BoltzSwapDocument FromSwapModel(BoltzSubmarineSwap swap, string walletId, string? projectId = null)
        {
            return new BoltzSwapDocument
            {
                SwapId = swap.Id,
                WalletId = walletId,
                ProjectId = projectId,
                Invoice = swap.Invoice,
                Address = swap.Address,
                LockupAddress = swap.LockupAddress,
                ExpectedAmount = swap.ExpectedAmount,
                InvoiceAmount = swap.InvoiceAmount,
                TimeoutBlockHeight = swap.TimeoutBlockHeight,
                SwapTree = swap.SwapTree,
                RefundPublicKey = swap.RefundPublicKey,
                ClaimPublicKey = swap.ClaimPublicKey,
                Preimage = swap.Preimage,
                PreimageHash = swap.PreimageHash,
                Status = swap.Status.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}


namespace Angor.Shared.Models
{
    public class SignatureRequest
    {
        public string investorNostrPubKey { get; set; }
        public decimal? AmountToInvest { get; set; }
        public DateTime TimeArrived { get; set; }
        public DateTime? TimeApproved { get; set; }
        public string? TransactionHex { get; set; }
        public string EventId { get; set; }
    }
}
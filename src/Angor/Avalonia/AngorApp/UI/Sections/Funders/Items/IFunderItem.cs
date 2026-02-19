namespace AngorApp.UI.Sections.Funders.Items
{
    public interface IFunderItem
    {
        public string Name { get; }
        public IAmountUI Amount { get; set; }
        public DateTimeOffset DateCreated { get; }
        public IEnhancedCommand<Result> Approve { get; }
        public IEnhancedCommand<Result> Reject { get; }
        public IEnhancedCommand OpenChat { get; }
        public FunderStatus Status { get; }
        public string InvestorNpub { get; }
    }
}
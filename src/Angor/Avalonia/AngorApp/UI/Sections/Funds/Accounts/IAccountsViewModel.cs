namespace AngorApp.UI.Sections.Funds.Accounts
{
    public interface IAccountsViewModel
    {
        public IEnumerable<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand<Result> ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }
    }
}
using System.Collections.ObjectModel;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public interface IAccountsViewModel
    {
        public ICollection<IAccountGroup> AccountGroups { get; }
        public IEnhancedCommand ImportAccount { get; }
        public IEnumerable<IAccountBalance> Balances { get; }
    }
}
namespace AngorApp.UI.Sections.Funds.Accounts
{
    public interface IAccountGroup
    {
        public IEnumerable<IAccount> Accounts { get; }
        public string Name { get; }
    }
}
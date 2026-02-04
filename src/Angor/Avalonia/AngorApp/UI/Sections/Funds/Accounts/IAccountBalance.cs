namespace AngorApp.UI.Sections.Funds.Accounts
{
    public interface IAccountBalance
    {
        public string Name { get; }
        public IObservable<IAmountUI> Balance { get; }
    }
}
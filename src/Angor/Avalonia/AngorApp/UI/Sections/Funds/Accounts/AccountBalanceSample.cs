namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountBalanceSample : IAccountBalance
    {
        public string Name { get; set; }
        public IObservable<IAmountUI> Balance { get; set; }
    }
}
namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountBalanceSample : IAccountBalance
    {
        public string Name { get; set; } = string.Empty;
        public IObservable<IAmountUI> Balance { get; set; } = null!;
    }
}
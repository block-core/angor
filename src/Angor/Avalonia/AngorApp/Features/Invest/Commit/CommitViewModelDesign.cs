namespace AngorApp.Features.Invest.Commit;

public class CommitViewModelDesign : ICommitViewModel
{
    public ReactiveCommand<Unit, Result<Guid>> RequestInvestment { get; }
    public long SatsToInvest { get; set; }
    public long Totalfee { get; set; }
    public IObservable<bool> IsInvesting { get; set; }
}
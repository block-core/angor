namespace AngorApp.Features.Invest.Commit;

public interface ICommitViewModel
{
    ReactiveCommand<Unit, Result<Guid>> RequestInvestment { get; }
    public long SatsToInvest { get; }
    public long Totalfee { get; }
}
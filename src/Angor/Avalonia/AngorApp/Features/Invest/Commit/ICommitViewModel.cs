namespace AngorApp.Features.Invest.Commit;

public interface ICommitViewModel
{
    ReactiveCommand<Unit, Result<Guid>> RequestInvestment { get; }
}
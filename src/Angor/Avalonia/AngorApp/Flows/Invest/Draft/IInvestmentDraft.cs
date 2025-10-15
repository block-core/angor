namespace AngorApp.Flows.Invest.Draft;

public interface IInvestmentDraft
{
    IAmountUI TransactionFee { get; }
    IAmountUI MinerFee { get; }
    IAmountUI AngorFee { get; }
    Task<Result<Guid>> Confirm();
}
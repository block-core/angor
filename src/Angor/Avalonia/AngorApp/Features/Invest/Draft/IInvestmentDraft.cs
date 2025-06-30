using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor.Operations;

namespace AngorApp.Features.Invest.Draft;

public interface IInvestmentDraft
{
    IAmountUI TransactionFee { get; }
    IAmountUI MinerFee { get; }
    IAmountUI AngorFee { get; }
    Task<Result<Guid>> Confirm();
}
using Angor.Sdk.Funding.Investor.Operations;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Investor.Domain;

public interface IPortfolioService
{
    Task<Result<InvestmentRecords>> GetByWalletId(string walletId);
    Task<Result> AddOrUpdate(string walletId, InvestmentRecord investment);
    Task<Result> RemoveInvestmentRecordAsync(string walletId, InvestmentRecord investment);
}
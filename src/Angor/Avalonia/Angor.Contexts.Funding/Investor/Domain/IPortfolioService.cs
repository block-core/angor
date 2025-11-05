using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Domain;

public interface IPortfolioService
{
    Task<Result<InvestmentRecords>> GetByWalletId(Guid walletId);
    Task<Result> Add(Guid walletId, InvestmentRecord investment);
    Task<Result> Update(Guid walletId, InvestmentRecord investment);
}
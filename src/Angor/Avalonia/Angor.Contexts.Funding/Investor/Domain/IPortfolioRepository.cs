using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Domain;

public interface IPortfolioRepository
{
    Task<Result<InvestmentRecords>> GetByWalletId(Guid walletId);
    Task<Result> Add(Guid walletId, InvestmentRecord investment);
}
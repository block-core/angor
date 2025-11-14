using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Domain;

public interface IPortfolioService
{
    Task<Result<InvestmentRecords>> GetByWalletId(string walletId);
    Task<Result> AddOrUpdate(string walletId, InvestmentRecord investment);
}
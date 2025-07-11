using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result<IEnumerable<InvestmentDto>>> GetByProjectId(ProjectId projectId);
    Task<Result<InvestmentRecords>> GetByWalletId(Guid walletId);
    Task<Result> Add(Guid walletId, Investment investment);
}
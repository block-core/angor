using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId);
    Task<Result<InvestmentRecords>> GetByWallet(Guid walletId);
    Task<Result> Add(Guid walletId, Investment investment);
}
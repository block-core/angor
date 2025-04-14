using Angor.Contexts.Funding.Investor.Dtos;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId);
    // Task<Result<PendingInvestment>> GetPendingInvestment(Guid walletId, ProjectId projectId);
    // Task<Result<SignedInvestment>> GetSignedInvestment(Guid walletId, ProjectId projectId);
    Task<Result> Add(Guid walletId, Investment investment);
}
using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId);
    Task<Result<PendingInvestment>> GetPendingInvestment(Guid walletId, ProjectId projectId);
    Task<Result<SignedInvestment>> GetSignedInvestment(Guid walletId, ProjectId projectId);
    Task<Result> Add(Guid walletId, Contexts.Projects.Domain.Investment investment);
}
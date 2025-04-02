using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IInvestmentRepository
{
    Task<Result> Add(Guid walletId, Investment investment);
    Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId);
    Task<Result<PendingInvestment>> GetPendingInvestment(Guid walletId, ProjectId projectId);
    Task<Result<SignedInvestment>> GetSignedInvestment(Guid walletId, ProjectId projectId);
}
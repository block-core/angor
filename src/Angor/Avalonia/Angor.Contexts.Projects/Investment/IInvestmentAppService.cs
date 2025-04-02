using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Investment;

public interface IInvestmentAppService
{
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
    Task<Result<PendingInvestment>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount);
}
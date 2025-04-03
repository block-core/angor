using Angor.Contexts.Funding.Investment.Commands.CreateInvestment;
using Angor.Contexts.Funding.Investment.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investment;

public interface IInvestmentAppService
{
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
    Task<Result<PendingInvestment>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount);
}
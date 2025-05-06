using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
    Task<Result<CreateInvestment.Draft>> CreateDraft(Guid sourceWalletId, ProjectId projectId, Amount amount);
    Task<Result<Guid>> RequestInvestment(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft);
    Task<Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments(Guid walletId, ProjectId projectId);
}
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount);
    Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft);
    Task<Result<IEnumerable<GetInvestments.Investment>>> GetInvestments(Guid walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, GetInvestments.Investment investment);
}
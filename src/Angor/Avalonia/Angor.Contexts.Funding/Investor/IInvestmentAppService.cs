using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate);
    Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft);
    Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid idValue);
    Task<Result> ConfirmInvestment(int investmentId);
    Task<Result<string>> CreateProject(Guid walletId, long selectedFee, CreateProjectDto project);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(Guid walletId);
}
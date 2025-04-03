using Angor.Contexts.Funding.Investor.CreateInvestment;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Requests.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
    Task<Result<InvestmentTransaction>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount);
    Task<Result<Guid>> RequestFounderSignatures(ProjectId projectId, InvestmentTransaction investmentTransaction);
}
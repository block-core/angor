using Angor.Contexts.Funding.Investor.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor;

public class RequestFounderSignaturesRequest(ProjectId projectId, InvestmentTransaction investmentTransaction) : IRequest<Result<FounderSignature>>
{
    public ProjectId ProjectId { get; } = projectId;
    public InvestmentTransaction InvestmentTransaction { get; } = investmentTransaction;
}
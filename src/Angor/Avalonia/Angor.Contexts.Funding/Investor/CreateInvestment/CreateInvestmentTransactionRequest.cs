using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.CreateInvestment;

public class CreateInvestmentTransactionRequest : IRequest<Result<InvestmentTransaction>>
{
    public Guid WalletId { get; }
    public ProjectId ProjectId { get; }
    public Amount Amount { get; }

    public CreateInvestmentTransactionRequest(Guid walletId, ProjectId projectId, Amount amount)
    {
        WalletId = walletId;
        ProjectId = projectId;
        Amount = amount;
    }
}
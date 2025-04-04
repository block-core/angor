using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Requests.CreateInvestment;

public class CreateInvestmentRequest : IRequest<Result<PendingInvestment>>
{
    public Guid WalletId { get; }
    public ProjectId ProjectId { get; }
    public Amount Amount { get; }

    public CreateInvestmentRequest(Guid walletId, ProjectId projectId, Amount amount)
    {
        WalletId = walletId;
        ProjectId = projectId;
        Amount = amount;
    }
}
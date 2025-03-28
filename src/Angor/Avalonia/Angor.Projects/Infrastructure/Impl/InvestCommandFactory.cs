using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared.ProtocolNew;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestCommandFactory(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider)
{
    public InvestCommand Create(Guid walletId, ProjectId projectId, Amount amount)
    {
        return new InvestCommand(
            projectRepository,
            investmentRepository, 
            investorTransactionActions,
            investorKeyProvider,
            walletId,
            projectId,
            amount);
    }
}
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.ProtocolNew;

namespace Angor.Contexts.Projects.Infrastructure.Impl;

public class InvestCommandFactory(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider,
    IWalletOperations walletOperations)
{
    public InvestCommand Create(Guid walletId, ProjectId projectId, Amount amount)
    {
        return new InvestCommand(
            projectRepository: projectRepository,
            investmentRepository: investmentRepository, 
            investorTransactionActions: investorTransactionActions,
            investorKeyProvider: investorKeyProvider,
            walletOperations: walletOperations,
            walletId: walletId,
            projectId: projectId, 
            amount: amount);
    }
}
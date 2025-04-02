using Angor.Contexts.Projects.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;

public interface ISignatureRequestService
{
    Task<Result> SendSignatureRequest(Guid walletId, string founderPubKey, ProjectId projectId, TransactionInfo signedTransaction);
}
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public interface ISignatureRequestService
{
    Task<Result> SendSignatureRequest(string walletId, string founderPubKey, ProjectId projectId, TransactionInfo signedTransaction);
}
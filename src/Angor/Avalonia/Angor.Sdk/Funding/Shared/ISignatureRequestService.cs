using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Shared;

public interface ISignatureRequestService
{
    Task<Result> SendSignatureRequest(WalletId walletId, string founderPubKey, ProjectId projectId, TransactionInfo signedTransaction);
}
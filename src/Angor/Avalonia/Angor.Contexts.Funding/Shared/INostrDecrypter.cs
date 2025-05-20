using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding;

public interface INostrDecrypter
{
    Task<Result<string>> Decrypt(Guid walletId, ProjectId projectId, InvestmentMessage nostrMessage);
}
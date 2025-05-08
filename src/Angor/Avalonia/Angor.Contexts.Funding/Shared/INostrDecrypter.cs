using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding;

public interface INostrDecrypter
{
    Task<Result<string>> Decrypt(Guid walletId, ProjectId projectId, NostrMessage nostrMessage);
}
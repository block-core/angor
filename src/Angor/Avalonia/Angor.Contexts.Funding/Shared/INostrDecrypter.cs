using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public interface INostrDecrypter
{
    Task<Result<string>> Decrypt(string walletId, ProjectId projectId, DirectMessage nostrMessage);
}
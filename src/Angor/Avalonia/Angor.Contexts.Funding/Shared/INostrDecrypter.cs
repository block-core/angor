using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public interface INostrDecrypter
{
    Task<Result<string>> Decrypt(WalletId walletId, ProjectId projectId, DirectMessage nostrMessage);
}
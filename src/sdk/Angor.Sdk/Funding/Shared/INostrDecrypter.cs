using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Primitives;

namespace Angor.Sdk.Funding.Shared;

public interface INostrDecrypter
{
    Task<Result<string>> Decrypt(WalletId walletId, ProjectId projectId, DirectMessage nostrMessage);
}
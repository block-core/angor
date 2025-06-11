using CSharpFunctionalExtensions;
using Nostr.Client.Messages;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface INostrService
{
    Task<Result<NostrOkResponse>> Send(NostrEvent nostrEvent);
}
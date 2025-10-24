using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;

namespace Angor.Contexts.Funding.Shared;

public class NostrDecrypter(IDerivationOperations derivationOperations, ISeedwordsProvider provider, IProjectRepository projectRepository) : INostrDecrypter
{
    public async Task<Result<string>> Decrypt(Guid walletId, ProjectId projectId, DirectMessage nostrMessage)
    {
        var sensitiveDataResult = await provider.GetSensitiveData(walletId);
        if (sensitiveDataResult.IsFailure)
            return Result.Failure<string>(sensitiveDataResult.Error);

        var projectResult = await projectRepository.GetAsync(projectId);
        if (projectResult.IsFailure)
            return Result.Failure<string>(projectResult.Error);

        var nostrPrivateKey = derivationOperations.DeriveProjectNostrPrivateKey(
                sensitiveDataResult.Value.ToWalletWords(), 
                projectResult.Value.FounderKey);
            

        var decryptResult = Result.Try(() =>
        {
            var bytes = nostrPrivateKey.ToBytes();
            var hex = Encoders.Hex.EncodeData(bytes);

            var nostrClientPrivateKey = NostrPrivateKey.FromHex(hex);
            
            var encryptedEvent = new NostrEncryptedEvent(nostrMessage.Content,
                new NostrEventTags(NostrEventTag.Profile(nostrClientPrivateKey.DerivePublicKey().Hex)))
            {
                Pubkey = nostrMessage.InvestorNostrPubKey,
            };

            return encryptedEvent.DecryptContent(nostrClientPrivateKey);
        });

        return decryptResult;
    }
}
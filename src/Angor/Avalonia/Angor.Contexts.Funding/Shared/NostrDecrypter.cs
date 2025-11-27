using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Services;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public class NostrDecrypter(IDerivationOperations derivationOperations, ISeedwordsProvider provider, IProjectService projectService,
    IEncryptionService encryptionService) : INostrDecrypter
{
    public async Task<Result<string>> Decrypt(WalletId walletId, ProjectId projectId, DirectMessage nostrMessage)
    {
        var sensitiveDataResult = await provider.GetSensitiveData(walletId.Value);
        if (sensitiveDataResult.IsFailure)
            return Result.Failure<string>(sensitiveDataResult.Error);

        var projectResult = await projectService.GetAsync(projectId);
        if (projectResult.IsFailure)
            return Result.Failure<string>(projectResult.Error);

        var nostrPrivateKey = derivationOperations.DeriveProjectNostrPrivateKey(
                sensitiveDataResult.Value.ToWalletWords(), 
                projectResult.Value.FounderKey);
            

        var decryptResult = Result.Try(() =>
        {
            var bytes = nostrPrivateKey.ToBytes();
            var hex = Encoders.Hex.EncodeData(bytes);

            var nostrPubKey = nostrPrivateKey.PubKey.ToHex()[2..];
            
            var isSender = nostrPubKey.Equals(nostrMessage.SenderNostrPubKey, StringComparison.OrdinalIgnoreCase);
            
            var otherPubKey = isSender ? projectResult.Value.NostrPubKey : nostrMessage.SenderNostrPubKey; //We assume all messages are between investor and project npub

            return encryptionService.DecryptNostrContentAsync(hex, otherPubKey, nostrMessage.Content);
        });

        return await decryptResult;
    }
}
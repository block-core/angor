using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public class NostrDecrypter(IDerivationOperations derivationOperations, IEncryptionService encryptionService, ISeedwordsProvider provider, IProjectRepository projectRepository) : INostrDecrypter
{
    public Task<Result<string>> Decrypt(Guid walletId, ProjectId projectId, DirectMessage nostrMessage)
    {
        return from sensitiveData in provider.GetSensitiveData(walletId)
            from project in projectRepository.Get(projectId)
            from nostrPrivateKey in Result.Try(() => derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveData.ToWalletWords(), project.FounderKey))
            from decrypted in Result.Try(() =>
            {
                var bytes = nostrPrivateKey.ToBytes();
                var hex = Encoders.Hex.EncodeData(bytes);
                return encryptionService.DecryptNostrContentAsync(hex, nostrMessage.InvestorNostrPubKey, nostrMessage.Content);
            })
            select decrypted;
    }
}
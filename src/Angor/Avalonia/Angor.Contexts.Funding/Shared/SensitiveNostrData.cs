using Angor.Contests.CrossCutting;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public class SensitiveNostrData : ISensitiveNostrData
{
    private readonly ISeedwordsProvider seedwordsProvider;
    private readonly IDerivationOperations derivationOperations;
    private readonly INetworkConfiguration networkConfiguration;

    public SensitiveNostrData(ISeedwordsProvider seedwordsProvider, IDerivationOperations derivationOperations, INetworkConfiguration networkConfiguration)
    {
        this.seedwordsProvider = seedwordsProvider;
        this.derivationOperations = derivationOperations;
        this.networkConfiguration = networkConfiguration;
    }
    
    // TODO: Check with David
    public Task<Result<string>> GetNostrPrivateKey(KeyIdentifier keyIdentifier)
    {
        return seedwordsProvider.GetSensitiveData(keyIdentifier.WalletId)
            .Map(tuple => tuple.ToWalletWords())
            .Map(walletWords => derivationOperations.DeriveProjectNostrPrivateKey(walletWords, keyIdentifier.FounderPubKey))
            .Map(key => key.ToHex(networkConfiguration.GetNetwork().Consensus.ConsensusFactory));
    }
}
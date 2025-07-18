using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP39;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.Protocol;

public class AngorTestData
{
    protected string angorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    
    protected Mock<INetworkConfiguration> _networkConfiguration;
    protected DerivationOperations _derivationOperations;

    protected AngorTestData()
    {
        _networkConfiguration = new Mock<INetworkConfiguration>();
        _networkConfiguration.Setup(_ => _.GetNetwork())
            .Returns(Networks.Bitcoin.Testnet());

        _networkConfiguration.Setup(_ => _.GetAngorInvestFeePercentage)
            .Returns(1);

        _derivationOperations = new DerivationOperations(new HdOperations(),
            new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
    }
    
    protected ProjectInfo GivenValidProjectInvestmentInfo( WalletWords? words = null, DateTime? startDate = null)
    {
        words ??= new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        startDate ??= DateTime.UtcNow;
        
        var projectInvestmentInfo = new ProjectInfo();
        projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
        projectInvestmentInfo.StartDate = startDate.Value;
        projectInvestmentInfo.ExpiryDate = startDate.Value.AddDays(5);
        projectInvestmentInfo.PenaltyDays = 10;
        projectInvestmentInfo.Stages = new List<Stage>
        {
            new() { AmountToRelease = 10, ReleaseDate = startDate.Value.AddDays(1) },
            new() { AmountToRelease = 50, ReleaseDate = startDate.Value.AddDays(2) },
            new() { AmountToRelease = 40, ReleaseDate = startDate.Value.AddDays(3) }
        };
        projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInvestmentInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
        projectInvestmentInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);
        
        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders { Threshold = 2 };
        return projectInvestmentInfo;
    }
}
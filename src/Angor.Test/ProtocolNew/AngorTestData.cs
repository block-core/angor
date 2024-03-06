using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.NBitcoin.BIP39;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.ProtocolNew;

public class AngorTestData
{
    protected string AngorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    
    protected Mock<INetworkConfiguration> NetworkConfiguration;
    protected DerivationOperations DerivationOperations;

    protected AngorTestData()
    {
        NetworkConfiguration = new Mock<INetworkConfiguration>();
        NetworkConfiguration.Setup(_ => _.GetNetwork())
            .Returns(Networks.Bitcoin.Testnet());
        
        DerivationOperations = new DerivationOperations(new HdOperations(),
            new NullLogger<DerivationOperations>(), NetworkConfiguration.Object);
    }
    
    protected ProjectInfo GivenValidProjectInvestmentInfo( WalletWords? words = null, DateTime? startDate = null)
    {
        words ??= new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        startDate ??= DateTime.UtcNow;
        
        var projectInvestmentInfo = new ProjectInfo();
        projectInvestmentInfo.TargetAmount = 3;
        projectInvestmentInfo.StartDate = startDate.Value;
        projectInvestmentInfo.ExpiryDate = startDate.Value.AddDays(5);
        projectInvestmentInfo.PenaltyDays = 10;
        projectInvestmentInfo.Stages = new List<Stage>
        {
            new() { AmountToRelease = (decimal)0.1, ReleaseDate = startDate.Value.AddDays(1) },
            new() { AmountToRelease = (decimal)0.5, ReleaseDate = startDate.Value.AddDays(2) },
            new() { AmountToRelease = (decimal)0.4, ReleaseDate = startDate.Value.AddDays(3) }
        };
        projectInvestmentInfo.FounderKey = DerivationOperations.DeriveFounderKey(words, 1);
        projectInvestmentInfo.FounderRecoveryKey = DerivationOperations.DeriveFounderRecoveryKey(words, 1);
        projectInvestmentInfo.ProjectIdentifier =
            DerivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, AngorRootKey);
        
        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders { Threshold = 2 };
        return projectInvestmentInfo;
    }
}
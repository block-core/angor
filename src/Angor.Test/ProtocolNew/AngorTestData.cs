using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.NBitcoin.BIP39;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.ProtocolNew;

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
        
        _derivationOperations = new DerivationOperations(new HdOperations(),
            new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
    }
    
    protected ProjectInfo GivenValidProjectInvestmentInfo( WalletWords? words = null)
    {
        words ??= new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };
        
        var projectInvestmentInfo = new ProjectInfo();
        projectInvestmentInfo.TargetAmount = 3;
        projectInvestmentInfo.StartDate = DateTime.UtcNow;
        projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInvestmentInfo.Stages = new List<Stage>
        {
            new() { AmountToRelease = (decimal)0.1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new() { AmountToRelease = (decimal)0.5, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new() { AmountToRelease = (decimal)0.4, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInvestmentInfo.ProjectIdentifier =
            _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);
        
        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders { Threshold = 2 };
        return projectInvestmentInfo;
    }
}
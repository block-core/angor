using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Utilities;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using Key = Blockcore.NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using Mnemonic = Blockcore.NBitcoin.BIP39.Mnemonic;
using Wordlist = Blockcore.NBitcoin.BIP39.Wordlist;
using WordCount = Blockcore.NBitcoin.BIP39.WordCount;

namespace Angor.Test.Protocol;

public class SeederRecoveryBugsTests
{
    private readonly Mock<INetworkConfiguration> _networkConfiguration;
    private readonly DerivationOperations _derivationOperations;
    private readonly SeederTransactionActions _sut;

    private readonly string _angorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

    public SeederRecoveryBugsTests()
    {
        _networkConfiguration = new Mock<INetworkConfiguration>();
        _networkConfiguration.Setup(x => x.GetNetwork()).Returns(Networks.Bitcoin.Testnet());
        _networkConfiguration.Setup(x => x.GetAngorInvestFeePercentage).Returns(1);

        _derivationOperations = new DerivationOperations(
            new HdOperations(), NullLogger<DerivationOperations>.Instance, _networkConfiguration.Object);

        var scriptBuilder = new InvestmentScriptBuilder(new SeederScriptTreeBuilder());
        var projectScripts = new ProjectScriptsBuilder(_derivationOperations);
        var taprootBuilder = new TaprootScriptBuilder();
        var txBuilder = new InvestmentTransactionBuilder(_networkConfiguration.Object, projectScripts, scriptBuilder, taprootBuilder);
        var spendingBuilder = new SpendingTransactionBuilder(_networkConfiguration.Object, projectScripts, scriptBuilder);

        _sut = new SeederTransactionActions(
            NullLogger<SeederTransactionActions>.Instance,
            scriptBuilder, projectScripts, spendingBuilder, txBuilder, taprootBuilder,
            _networkConfiguration.Object);
    }

    [Fact]
    public void AddSignaturesToRecoverSeederFundsTransaction_FundProject_ShouldSignAllInputs()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };
        var projectInfo = BuildProjectInfo(words);
        projectInfo.ProjectType = ProjectType.Fund;
        projectInfo.Stages = new List<Stage>();
        projectInfo.DynamicStagePatterns = new List<DynamicStagePattern>
        {
            new DynamicStagePattern
            {
                PatternId = 0,
                StageCount = 3,
                Frequency = StageFrequency.Monthly,
                PayoutDayType = PayoutDayType.FromStartDate
            }
        };

        var seederKey = new Key();
        var seederSecret = new Key();
        var secretHash = Hashes.Hash256(seederSecret.ToBytes());

        projectInfo.ProjectSeeders = new ProjectSeeders
        {
            Threshold = 1,
            SecretHashes = { secretHash.ToString() }
        };

        var fundingParams = new FundingParameters
        {
            InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
            HashOfSecret = new Blockcore.NBitcoin.uint256(secretHash.ToString()),
            TotalInvestmentAmount = Money.Coins(1).Satoshi,
            PatternId = 0,
            InvestmentStartDate = DateTime.UtcNow.Date
        };

        var investmentTx = _sut.CreateInvestmentTransaction(projectInfo, fundingParams);
        var taprootOutputCount = investmentTx.Outputs.AsIndexedOutputs().Count(o => o.IsTaprooOutput());
        Assert.Equal(3, taprootOutputCount);

        var recoveryTx = _sut.AddSignaturesToRecoverSeederFundsTransaction(
            projectInfo,
            investmentTx,
            Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
            BuildDummyFounderSignatures(taprootOutputCount),
            Encoders.Hex.EncodeData(seederKey.ToBytes()),
            Encoders.Hex.EncodeData(seederSecret.ToBytes()));

        Assert.Equal(taprootOutputCount, recoveryTx.Inputs.Count);
        Assert.All(recoveryTx.Inputs, input =>
            Assert.False(input.WitScript == null || input.WitScript == Blockcore.Consensus.TransactionInfo.WitScript.Empty));
    }

    [Fact]
    public void AddSignaturesToRecoverSeederFundsTransaction_ShouldSignInputForEveryStage()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };
        var projectInfo = BuildProjectInfo(words);

        var seederKey = new Key();
        var seederSecret = new Key();
        var secretHash = Hashes.Hash256(seederSecret.ToBytes());

        projectInfo.ProjectSeeders = new ProjectSeeders
        {
            Threshold = 1,
            SecretHashes = { secretHash.ToString() }
        };

        var investmentTx = _sut.CreateInvestmentTransaction(
            projectInfo,
            Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
            new Blockcore.NBitcoin.uint256(secretHash.ToString()),
            Money.Coins(1).Satoshi);

        var taprootOutputCount = investmentTx.Outputs.AsIndexedOutputs().Count(o => o.IsTaprooOutput());
        Assert.Equal(3, taprootOutputCount);

        var recoveryTx = _sut.AddSignaturesToRecoverSeederFundsTransaction(
            projectInfo,
            investmentTx,
            Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
            BuildDummyFounderSignatures(taprootOutputCount),
            Encoders.Hex.EncodeData(seederKey.ToBytes()),
            Encoders.Hex.EncodeData(seederSecret.ToBytes()));

        Assert.Equal(taprootOutputCount, recoveryTx.Inputs.Count);
        Assert.All(recoveryTx.Inputs, input =>
            Assert.False(input.WitScript == null || input.WitScript == Blockcore.Consensus.TransactionInfo.WitScript.Empty));
    }

    private ProjectInfo BuildProjectInfo(WalletWords words)
    {
        var founderKey = _derivationOperations.DeriveFounderKey(words, 1);
        var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, founderKey);
        var projectIdentifier = _derivationOperations.DeriveAngorKey(_angorRootKey, founderKey);

        return new ProjectInfo
        {
            TargetAmount = Money.Coins(3).Satoshi,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            PenaltyDays = 10,
            FounderKey = founderKey,
            FounderRecoveryKey = founderRecoveryKey,
            ProjectIdentifier = projectIdentifier,
            ProjectSeeders = new ProjectSeeders { Threshold = 1 },
            Stages = new List<Stage>
            {
                new() { AmountToRelease = 40, ReleaseDate = DateTime.UtcNow.AddDays(10) },
                new() { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddDays(20) },
                new() { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddDays(30) }
            }
        };
    }

    private static SignatureInfo BuildDummyFounderSignatures(int stageCount)
    {
        var info = new SignatureInfo();
        for (var i = 0; i < stageCount; i++)
        {
            info.Signatures.Add(new SignatureInfoItem
            {
                StageIndex = i,
                Signature = Encoders.Hex.EncodeData(new byte[64])
            });
        }
        return info;
    }
}

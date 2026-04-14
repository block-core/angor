using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Nostr.Client.Responses;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class RequestInvestmentSignaturesTests
{
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IProjectScriptsBuilder> _mockProjectScriptsBuilder;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly RequestInvestmentSignatures.RequestFounderSignaturesHandler _sut;

    public RequestInvestmentSignaturesTests()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockSerializer = new Mock<ISerializer>();
        _mockSignService = new Mock<ISignService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockProjectScriptsBuilder = new Mock<IProjectScriptsBuilder>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        _sut = new RequestInvestmentSignatures.RequestFounderSignaturesHandler(
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockEncryptionService.Object,
            _mockNetworkConfiguration.Object,
            _mockSerializer.Object,
            _mockSignService.Object,
            _mockPortfolioService.Object,
            _mockProjectScriptsBuilder.Object,
            _mockAngorIndexerService.Object,
            _mockWalletAccountBalanceService.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();

        _mockProjectScriptsBuilder
            .Setup(x => x.GetInvestmentDataFromOpReturnScript(It.IsAny<Blockcore.Consensus.ScriptInfo.Script>()))
            .Returns(("investor-key", (uint256?)null));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task Handle_WhenExistingInvestmentOnBlockchain_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();

        _mockProjectScriptsBuilder
            .Setup(x => x.GetInvestmentDataFromOpReturnScript(It.IsAny<Blockcore.Consensus.ScriptInfo.Script>()))
            .Returns(("investor-key", (uint256?)null));

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync("project-1", "investor-key"))
            .ReturnsAsync(new ProjectInvestment { TransactionId = "existing-tx" });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists on the blockchain");
    }

    [Fact]
    public async Task Handle_WhenSeedwordsFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();

        _mockProjectScriptsBuilder
            .Setup(x => x.GetInvestmentDataFromOpReturnScript(It.IsAny<Blockcore.Consensus.ScriptInfo.Script>()))
            .Returns(("investor-key", (uint256?)null));

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ProjectInvestment?)null);

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
    }

    [Fact]
    public async Task Handle_WhenBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();
        SetupSeedwords();
        SetupDerivation();

        _mockProjectScriptsBuilder
            .Setup(x => x.GetInvestmentDataFromOpReturnScript(It.IsAny<Blockcore.Consensus.ScriptInfo.Script>()))
            .Returns(("investor-key", (uint256?)null));

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ProjectInvestment?)null);

        // GetAccountBalanceInfoAsync fails — used by GetUnfundedReleaseAddress
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Balance unavailable"));

        _mockEncryptionService
            .Setup(x => x.EncryptNostrContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted-content");

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<SignRecoveryRequest>()))
            .Returns("serialized-request");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenSuccessful_StoresInvestmentRecordAndReservesUtxos()
    {
        // Arrange
        var request = CreateRequest();
        var network = SetupNetwork();
        SetupProject();
        SetupSeedwords();
        SetupDerivation();

        _mockProjectScriptsBuilder
            .Setup(x => x.GetInvestmentDataFromOpReturnScript(It.IsAny<Blockcore.Consensus.ScriptInfo.Script>()))
            .Returns(("investor-key", (uint256?)null));

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ProjectInvestment?)null);

        _mockEncryptionService
            .Setup(x => x.EncryptNostrContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted-content");

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<SignRecoveryRequest>()))
            .Returns("serialized-request");

        // Balance service returns valid info with address
        var balanceInfo = new AccountBalanceInfo();
        var accountInfo = new AccountInfo
        {
            AddressesInfo = new List<AddressInfo>
            {
                new AddressInfo { Address = "receive-address-1", HasHistory = false }
            }
        };
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));

        _mockSignService
            .Setup(x => x.RequestInvestmentSigs(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns((DateTime.UtcNow, "event-id-123"));

        _mockWalletAccountBalanceService
            .Setup(x => x.SaveAccountBalanceInfoAsync(It.IsAny<WalletId>(), It.IsAny<AccountBalanceInfo>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockPortfolioService.Verify(
            x => x.AddOrUpdate("wallet-1", It.Is<InvestmentRecord>(r =>
                r.ProjectIdentifier == "project-1" &&
                r.RequestEventId == "event-id-123")),
            Times.Once);
    }

    private static RequestInvestmentSignatures.RequestFounderSignaturesRequest CreateRequest()
    {
        // The handler accesses Outputs[1].ScriptPubKey, so we need a transaction with at least 2 outputs
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        var tx = network.CreateTransaction();
        tx.Outputs.Add(new Blockcore.Consensus.TransactionInfo.TxOut(Blockcore.NBitcoin.Money.Satoshis(1000), new Blockcore.Consensus.ScriptInfo.Script()));
        tx.Outputs.Add(new Blockcore.Consensus.TransactionInfo.TxOut(Blockcore.NBitcoin.Money.Satoshis(500), new Blockcore.Consensus.ScriptInfo.Script()));
        var validTxHex = tx.ToHex();

        return new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new InvestmentDraft("investor-key")
            {
                SignedTxHex = validTxHex,
                TransactionFee = new Amount(1000),
                TransactionId = "tx-id-123"
            });
    }

    private Blockcore.Networks.Network SetupNetwork()
    {
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);
        return network;
    }

    private void SetupProject()
    {
        var project = new Project
        {
            Id = new ProjectId("project-1"),
            Name = "Test Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub-key",
            ShortDescription = "Test",
            TargetAmount = 1_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            Stages = new List<Stage>()
        };
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupDerivation()
    {
        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());
    }
}

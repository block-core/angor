using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Founder;

public class SpendStageFundsTests
{
    private readonly Mock<IFounderTransactionActions> _mockFounderTransactionActions;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<IWalletOperations> _mockWalletOperations;
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _mockDerivedProjectKeysCollection;
    private readonly Mock<ILogger<SpendStageFunds.SpendStageFundsHandler>> _mockLogger;
    private readonly SpendStageFunds.SpendStageFundsHandler _sut;

    public SpendStageFundsTests()
    {
        _mockFounderTransactionActions = new Mock<IFounderTransactionActions>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockWalletOperations = new Mock<IWalletOperations>();
        _mockDerivedProjectKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();
        _mockLogger = new Mock<ILogger<SpendStageFunds.SpendStageFundsHandler>>();

        _sut = new SpendStageFunds.SpendStageFundsHandler(
            _mockFounderTransactionActions.Object,
            _mockNetworkConfiguration.Object,
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _mockDerivationOperations.Object,
            _mockSeedwordsProvider.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object,
            _mockWalletOperations.Object,
            _mockDerivedProjectKeysCollection.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenMultipleStages_ReturnsFailure()
    {
        // Arrange
        var toSpend = new List<SpendTransactionDto>
        {
            new SpendTransactionDto { InvestorAddress = "addr1", StageId = 0 },
            new SpendTransactionDto { InvestorAddress = "addr2", StageId = 1 }
        };

        var request = new SpendStageFunds.SpendStageFundsRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new FeeEstimation { Confirmations = 1, FeeRate = 10 },
            toSpend);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("one stage at a time");
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);

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
    public async Task Handle_WhenProjectKeysNotInStorage_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();

        // Keys not found in storage
        _mockDerivedProjectKeysCollection
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<DerivedProjectKeys>("Not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project keys not found");
    }

    [Fact]
    public async Task Handle_WhenProjectKeyExistsButNoMatchingProject_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();

        // Storage has keys but not for our project
        var storedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys
                {
                    ProjectIdentifier = "other-project",
                    FounderKey = "some-key",
                    Index = 0
                }
            }
        };
        _mockDerivedProjectKeysCollection
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(storedKeys));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project keys not found");
    }

    [Fact]
    public async Task Handle_WhenBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupNetwork();
        SetupProject();
        SetupProjectKeys();
        SetupSeedwords();

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ProjectInvestment?)null);

        _mockTransactionService
            .Setup(x => x.GetTransactionHexByIdAsync(It.IsAny<string>()))
            .ReturnsAsync("tx-hex");

        _mockWalletAccountBalanceService
            .Setup(x => x.RefreshAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Balance unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Could not get an unfunded release address");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsTransactionDraft()
    {
        // Arrange
        var request = CreateRequest();
        var network = SetupNetwork();
        SetupProject();
        SetupProjectKeys();
        SetupSeedwords();

        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ProjectInvestment { TransactionId = "tx-id-1" });

        _mockTransactionService
            .Setup(x => x.GetTransactionHexByIdAsync(It.IsAny<string>()))
            .ReturnsAsync("tx-hex-data");

        var balanceInfo = new AccountBalanceInfo();
        var accountInfo = new AccountInfo
        {
            ChangeAddressesInfo = new List<AddressInfo>
            {
                new AddressInfo { Address = "tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", HasHistory = false }
            }
        };
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.RefreshAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));

        var signedTx = network.CreateTransaction();
        _mockFounderTransactionActions
            .Setup(x => x.SpendFounderStage(
                It.IsAny<ProjectInfo>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<Blockcore.Consensus.ScriptInfo.Script>(),
                It.IsAny<string>(),
                It.IsAny<FeeEstimation>()))
            .Returns(new Angor.Shared.Models.TransactionInfo
            {
                Transaction = signedTx,
                TransactionFee = 1500
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionDraft.Should().NotBeNull();
        result.Value.TransactionDraft.TransactionFee.Should().Be(new Amount(1500));
    }

    private static SpendStageFunds.SpendStageFundsRequest CreateRequest()
    {
        var toSpend = new List<SpendTransactionDto>
        {
            new SpendTransactionDto { InvestorAddress = "investor-addr-1", StageId = 0 }
        };

        return new SpendStageFunds.SpendStageFundsRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new FeeEstimation { Confirmations = 1, FeeRate = 10 },
            toSpend);
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

    private void SetupProjectKeys()
    {
        var storedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys
                {
                    ProjectIdentifier = "project-1",
                    FounderKey = "founder-key",
                    Index = 0
                }
            }
        };
        _mockDerivedProjectKeysCollection
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(storedKeys));
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveFounderPrivateKey(It.IsAny<WalletWords>(), It.IsAny<int>()))
            .Returns(new Key());
    }
}

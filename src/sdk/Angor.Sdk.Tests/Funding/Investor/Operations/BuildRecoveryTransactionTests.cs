using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class BuildRecoveryTransactionTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<IWalletOperations> _mockWalletOperations;
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IEncryptionService> _mockDecrypter;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly BuildRecoveryTransaction.BuildRecoveryTransactionHandler _sut;

    public BuildRecoveryTransactionTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockProjectService = new Mock<IProjectService>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockWalletOperations = new Mock<IWalletOperations>();
        _mockSignService = new Mock<ISignService>();
        _mockDecrypter = new Mock<IEncryptionService>();
        _mockSerializer = new Mock<ISerializer>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        _sut = new BuildRecoveryTransaction.BuildRecoveryTransactionHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockProjectService.Object,
            _mockInvestorTransactionActions.Object,
            _mockPortfolioService.Object,
            _mockNetworkConfiguration.Object,
            _mockWalletOperations.Object,
            _mockSignService.Object,
            _mockDecrypter.Object,
            _mockSerializer.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object);
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

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
        SetupSeedwords();

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Balance unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Balance unavailable");
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();

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
    public async Task Handle_WhenPortfolioFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();
        SetupProject();

        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task Handle_WhenNoInvestmentFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();
        SetupProject();

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>()
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No investment found");
    }

    [Fact]
    public async Task Handle_WhenNoFounderSignaturesFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();
        SetupProject();
        SetupInvestment();
        SetupNetwork();

        _mockDerivationOperations
            .Setup(x => x.DeriveInvestorPrivateKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new Key());

        _mockDerivationOperations
            .Setup(x => x.DeriveNostrPubKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns("derived-nostr-pub");

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());

        // LookupSignatureForInvestmentRequest ends without finding signatures
        _mockSignService
            .Setup(x => x.LookupSignatureForInvestmentRequest(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
                It.IsAny<Func<string, Task>>(), It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Func<string, Task>, Action>(
                (pubKey, projPub, time, eventId, onContent, onEnd) =>
                {
                    onEnd();
                });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No founder signatures found");
    }

    [Fact]
    public async Task Handle_WhenTransactionInfoNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();
        SetupProject();
        SetupInvestment();
        var network = SetupNetwork();

        _mockDerivationOperations
            .Setup(x => x.DeriveInvestorPrivateKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new Key());

        _mockDerivationOperations
            .Setup(x => x.DeriveNostrPubKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns("derived-nostr-pub");

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());

        // LookupSignatureForInvestmentRequest finds valid signatures
        var signatureInfo = new SignatureInfo();
        _mockSignService
            .Setup(x => x.LookupSignatureForInvestmentRequest(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
                It.IsAny<Func<string, Task>>(), It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Func<string, Task>, Action>(
                (pubKey, projPub, time, eventId, onContent, onEnd) =>
                {
                    // Simulate receiving valid content
                    onContent("encrypted-signatures").Wait();
                });

        _mockDecrypter
            .Setup(x => x.DecryptNostrContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("decrypted-sig-json");

        _mockSerializer
            .Setup(x => x.Deserialize<SignatureInfo>(It.IsAny<string>()))
            .Returns(signatureInfo);

        _mockInvestorTransactionActions
            .Setup(x => x.CheckInvestorRecoverySignatures(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>()))
            .Returns(true);

        _mockInvestorTransactionActions
            .Setup(x => x.AddSignaturesToRecoverSeederFundsTransaction(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>(),
                It.IsAny<string>()))
            .Returns(network.CreateTransaction());

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((QueryTransaction?)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Could not find transaction info");
    }

    private static BuildRecoveryTransaction.BuildRecoveryTransactionRequest CreateRequest()
    {
        return new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new DomainFeerate(10));
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupBalanceService()
    {
        var balanceInfo = new AccountBalanceInfo();
        var accountInfo = new AccountInfo
        {
            ChangeAddressesInfo = new List<AddressInfo>
            {
                new AddressInfo { Address = "change-address-1", HasHistory = false }
            }
        };
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));
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

    private void SetupInvestment()
    {
        // Must be valid hex that network.CreateTransaction() can parse
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        var validTxHex = network.CreateTransaction().ToHex();

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHex = validTxHex,
            InvestmentTransactionHash = "tx-hash",
            RequestEventId = "request-event-id",
            RequestEventTime = DateTime.UtcNow
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));
    }

    private Blockcore.Networks.Network SetupNetwork()
    {
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);
        return network;
    }
}

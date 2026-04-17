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

public class BuildPenaltyReleaseTransactionTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionHandler _sut;

    public BuildPenaltyReleaseTransactionTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockProjectService = new Mock<IProjectService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        _sut = new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockNetworkConfiguration.Object,
            _mockInvestorTransactionActions.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

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
    public async Task Handle_WhenPortfolioLookupFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

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

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
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
    public async Task Handle_WhenNoRecoveryTransaction_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        SetupInvestment(recoveryTxId: null, recoveryReleaseTxId: null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("recovery must be done before releasing from penalty");
    }

    [Fact]
    public async Task Handle_WhenPenaltyReleaseAlreadyPublished_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        SetupInvestment(recoveryTxId: "recovery-tx", recoveryReleaseTxId: "already-released");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Penalty release transaction has already been published");
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        SetupInvestment(recoveryTxId: "recovery-tx", recoveryReleaseTxId: null);

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

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        SetupInvestment(recoveryTxId: "recovery-tx", recoveryReleaseTxId: null);
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
    public async Task Handle_WhenInvestmentTransactionHexMissing_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        SetupInvestment(recoveryTxId: "recovery-tx", recoveryReleaseTxId: null, investmentTxHex: null);
        SetupSeedwords();
        SetupBalanceService(changeAddress: "change-addr");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Investment transaction hex not found");
    }

    private static BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest CreateRequest()
    {
        return new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(
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

    private void SetupBalanceService(string? changeAddress = null)
    {
        var accountInfo = new AccountInfo();
        if (changeAddress != null)
        {
            accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress });
        }
        var balanceInfo = new AccountBalanceInfo();
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));
    }

    private void SetupInvestment(string? recoveryTxId, string? recoveryReleaseTxId, string? investmentTxHex = "tx-hex-data")
    {
        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHash = "tx-hash-123",
            InvestmentTransactionHex = investmentTxHex,
            RecoveryTransactionId = recoveryTxId,
            RecoveryReleaseTransactionId = recoveryReleaseTxId
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));
    }

    private static Project CreateTestProject()
    {
        return new Project
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
    }
}

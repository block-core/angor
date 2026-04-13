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

public class BuildEndOfProjectClaimTests
{
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly BuildEndOfProjectClaim.BuildEndOfProjectClaimHandler _sut;

    public BuildEndOfProjectClaimTests()
    {
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockProjectService = new Mock<IProjectService>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        _sut = new BuildEndOfProjectClaim.BuildEndOfProjectClaimHandler(
            _mockDerivationOperations.Object,
            _mockProjectService.Object,
            _mockInvestorTransactionActions.Object,
            _mockPortfolioService.Object,
            _mockSeedwordsProvider.Object,
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
    public async Task Handle_WhenPortfolioFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();

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
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSeedwords();
        SetupBalanceService();
        SetupInvestmentWithHex("tx-hex-data");

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((QueryTransaction?)null);

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    private static BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest CreateRequest()
    {
        return new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(
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
        balanceInfo.UpdateAccountBalanceInfo(new AccountInfo(), new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));
    }

    private void SetupInvestmentWithHex(string? txHex)
    {
        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHash = "tx-hash-123",
            InvestmentTransactionHex = txHex
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Angor.Sdk.Tests.Funding.Founder;

public class CreateProjectTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IFounderTransactionActions> _mockFounderTransactionActions;
    private readonly Mock<IWalletOperations> _mockWalletOperations;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<IFounderProjectsService> _mockFounderProjectsService;
    private readonly Mock<ILogger<CreateProjectConstants.CreateProject.CreateProjectHandler>> _mockLogger;
    private readonly CreateProjectConstants.CreateProject.CreateProjectHandler _sut;

    public CreateProjectTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockFounderTransactionActions = new Mock<IFounderTransactionActions>();
        _mockWalletOperations = new Mock<IWalletOperations>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockFounderProjectsService = new Mock<IFounderProjectsService>();
        _mockLogger = new Mock<ILogger<CreateProjectConstants.CreateProject.CreateProjectHandler>>();

        _sut = new CreateProjectConstants.CreateProject.CreateProjectHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockFounderTransactionActions.Object,
            _mockWalletOperations.Object,
            _mockWalletAccountBalanceService.Object,
            _mockFounderProjectsService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectSeedDtoIsNull_ReturnsFailure()
    {
        // Arrange
        var request = new CreateProjectConstants.CreateProject.CreateProjectRequest(
            new WalletId("wallet-1"),
            10,
            CreateProjectDto(),
            "event-123",
            null!);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("FounderKeys cannot be null");
    }

    [Fact]
    public async Task Handle_WhenBalanceServiceFails_ThrowsException()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSeedwords();

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Balance unavailable"));

        // Act
        var act = () => _sut.Handle(request, CancellationToken.None);

        // Assert — the handler throws InvalidOperationException (not a Result.Failure)
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WhenSuccessful_PersistsFounderProjectRecord()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSuccessfulFlow();

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFounderProjectsService.Verify(
            x => x.Add("wallet-1", It.Is<FounderProjectRecord>(
                r => r.ProjectIdentifier == "project-id")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFounderProjectPersistFails_StillReturnsSuccess()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSuccessfulFlow();

        _mockFounderProjectsService
            .Setup(x => x.Add("wallet-1", It.IsAny<FounderProjectRecord>()))
            .ReturnsAsync(Result.Failure("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("Failure to persist record should only log a warning");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsTransactionDraft()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSuccessfulFlow();

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionDraft.Should().NotBeNull();
        result.Value.TransactionDraft.SignedTxHex.Should().NotBeNullOrEmpty();
        result.Value.TransactionDraft.TransactionId.Should().NotBeNullOrEmpty();
    }

    private static CreateProjectConstants.CreateProject.CreateProjectRequest CreateValidRequest()
    {
        return new CreateProjectConstants.CreateProject.CreateProjectRequest(
            new WalletId("wallet-1"),
            10,
            CreateProjectDto(),
            "event-123",
            new ProjectSeedDto("founder-key", "recovery-key", "nostr-pub-key", "project-id"));
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupSuccessfulFlow()
    {
        SetupSeedwords();

        var accountInfo = new AccountInfo();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = "change-addr" });
        var balanceInfo = new AccountBalanceInfo();
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));

        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        var tx = network.CreateTransaction();
        _mockFounderTransactionActions
            .Setup(x => x.CreateNewProjectTransaction(
                It.IsAny<string>(), It.IsAny<Script>(), It.IsAny<long>(), It.IsAny<short>(), It.IsAny<string>()))
            .Returns(tx);

        var transactionInfo = new TransactionInfo
        {
            Transaction = tx,
            TransactionFee = 1000
        };
        _mockWalletOperations
            .Setup(x => x.AddInputsAndSignTransaction(
                It.IsAny<string>(), It.IsAny<Transaction>(), It.IsAny<WalletWords>(),
                It.IsAny<AccountInfo>(), It.IsAny<long>()))
            .Returns(transactionInfo);

        _mockDerivationOperations
            .Setup(x => x.AngorKeyToScript(It.IsAny<string>()))
            .Returns(new Script());

        _mockFounderProjectsService
            .Setup(x => x.Add(It.IsAny<string>(), It.IsAny<FounderProjectRecord>()))
            .ReturnsAsync(Result.Success());
    }

    private static CreateProjectDto CreateProjectDto()
    {
        return new CreateProjectDto
        {
            ProjectName = "Test Project",
            Description = "A test project",
            AvatarUri = "https://example.com/avatar.png",
            BannerUri = "https://example.com/banner.png",
            Sats = 1_000_000,
            StartDate = DateTime.UtcNow,
            TargetAmount = new Amount(1_000_000),
            PenaltyDays = 30,
            Stages = new List<CreateProjectStageDto>
            {
                new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), 50),
                new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)), 50)
            }
        };
    }
}

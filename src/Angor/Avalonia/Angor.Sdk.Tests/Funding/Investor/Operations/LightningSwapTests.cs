﻿using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for CreateLightningSwapForInvestment handler
/// </summary>
public class CreateLightningSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>> _mockLogger;
    private readonly CreateLightningSwapForInvestment.CreateLightningSwapHandler _handler;

    public CreateLightningSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockLogger = new Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>>();

        _handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockSwapStorageService.Object,
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockLogger.Object);
    }

    private void SetupSuccessfulDependencies(ProjectId projectId)
    {
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            FounderKey = "02abc123founderkey"
        };

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(project));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(("word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 word11 word12", Maybe<string>.None)));

        _mockDerivationOperations
            .Setup(x => x.DeriveInvestorKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns("02refundpubkey123456789");
    }

    [Fact]
    public async Task CreateSwap_WithValidAmount_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("test-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        SetupSuccessfulDependencies(projectId);

        var expectedSwap = new BoltzSubmarineSwap
        {
            Id = "swap-123",
            Invoice = "lnbc1000n1...",
            Address = address,
            ExpectedAmount = 99000,
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(amount.Sats);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("swap-123", result.Value.Swap.Id);
        Assert.Equal("lnbc1000n1...", result.Value.Swap.Invoice);
    }

    [Fact]
    public async Task CreateSwap_BoltzServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("test-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        SetupSuccessfulDependencies(projectId);

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<BoltzSubmarineSwap>("Boltz API error"));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(amount.Sats);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Boltz API error", result.Error);
    }

    [Fact]
    public async Task CreateSwap_ProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("non-existent-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Project not found", result.Error);
    }

    [Fact]
    public async Task CreateSwap_WalletDataNotAvailable_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("test-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            FounderKey = "02abc123founderkey"
        };

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(project));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<(string, Maybe<string>)>("Wallet locked"));

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Wallet", result.Error);
    }
}

/// <summary>
/// Unit tests for MonitorLightningSwap handler
/// </summary>
public class MonitorLightningSwapTests
{
    private readonly Mock<IBoltzWebSocketClient> _mockWebSocketClient;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>> _mockLogger;
    private readonly MonitorLightningSwap.MonitorLightningSwapHandler _handler;

    public MonitorLightningSwapTests()
    {
        _mockWebSocketClient = new Mock<IBoltzWebSocketClient>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>>();

        _handler = new MonitorLightningSwap.MonitorLightningSwapHandler(
            _mockWebSocketClient.Object,
            _mockSwapStorageService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenSwapAlreadyClaimed_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        var completedStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionClaimed,
            TransactionId = "tx123"
        };

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(completedStatus));

        _mockSwapStorageService
            .Setup(x => x.UpdateSwapStatusAsync(swapId, walletId.Value, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("tx123", result.Value.ClaimTransactionId);
    }

    [Fact]
    public async Task Handle_WhenSwapFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BoltzSwapStatus>("Swap expired"));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error);
    }

    [Fact]
    public async Task Handle_WhenFundsLocked_ClaimsAndReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        var lockedStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionMempool,
            TransactionId = "lockup-tx-123",
            TransactionHex = "0100..."
        };

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(lockedStatus));

        _mockSwapStorageService
            .Setup(x => x.UpdateSwapStatusAsync(swapId, walletId.Value, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        _mockSwapStorageService
            .Setup(x => x.MarkSwapClaimedAsync(swapId, walletId.Value, It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var claimResponse = new ClaimLightningSwap.ClaimLightningSwapResponse("claim-tx-123", "signed-hex");
        _mockMediator
            .Setup(x => x.Send(It.IsAny<ClaimLightningSwap.ClaimLightningSwapByIdRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(claimResponse));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("claim-tx-123", result.Value.ClaimTransactionId);
        
        _mockMediator.Verify(x => x.Send(
            It.Is<ClaimLightningSwap.ClaimLightningSwapByIdRequest>(r => r.SwapId == swapId),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockSwapStorageService.Verify(x => x.MarkSwapClaimedAsync(swapId, walletId.Value, "claim-tx-123"), Times.Once);
    }
}

/// <summary>
/// Unit tests for SwapState extension methods
/// </summary>
public class SwapStateExtensionTests
{
    [Theory]
    [InlineData(SwapState.TransactionClaimed, true)]
    [InlineData(SwapState.InvoicePaid, false)]
    [InlineData(SwapState.TransactionMempool, false)]
    [InlineData(SwapState.Created, false)]
    public void IsComplete_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsComplete());
    }

    [Theory]
    [InlineData(SwapState.SwapExpired, true)]
    [InlineData(SwapState.InvoiceExpired, true)]
    [InlineData(SwapState.InvoiceFailedToPay, true)]
    [InlineData(SwapState.TransactionRefunded, true)]
    [InlineData(SwapState.InvoicePaid, false)]
    [InlineData(SwapState.TransactionClaimed, false)]
    public void IsFailed_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsFailed());
    }

    [Theory]
    [InlineData(SwapState.Created, true)]
    [InlineData(SwapState.InvoicePaid, true)]
    [InlineData(SwapState.TransactionMempool, true)]
    [InlineData(SwapState.TransactionConfirmed, true)]
    [InlineData(SwapState.TransactionClaimed, false)]
    [InlineData(SwapState.SwapExpired, false)]
    public void IsPending_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsPending());
    }
}



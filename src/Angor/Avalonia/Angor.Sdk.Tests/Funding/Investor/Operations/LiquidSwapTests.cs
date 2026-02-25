using Angor.Sdk.Common;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for CreateLiquidSwapForInvestment handler
/// </summary>
public class CreateLiquidSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ILogger<CreateLiquidSwapForInvestment.CreateLiquidSwapHandler>> _mockLogger;
    private readonly CreateLiquidSwapForInvestment.CreateLiquidSwapHandler _handler;

    public CreateLiquidSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockLogger = new Mock<ILogger<CreateLiquidSwapForInvestment.CreateLiquidSwapHandler>>();

        _handler = new CreateLiquidSwapForInvestment.CreateLiquidSwapHandler(
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
            .Returns("02claimpubkey123456789");
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
            Id = "liquid-swap-123",
            Invoice = string.Empty, // No Lightning invoice for Liquid swaps
            Address = address,
            LockupAddress = "tlq1qqtestliquidaddress",
            ExpectedAmount = 99000,
            InvoiceAmount = 101000,
            BlindingKey = "testblindingkey",
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.CreateLiquidToBtcSwapAsync(address, It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        _mockBoltzService
            .Setup(x => x.CalculateLiquidAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(101000L);

        _mockSwapStorageService
            .Setup(x => x.SaveSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var request = new CreateLiquidSwapForInvestment.CreateLiquidSwapRequest(
            walletId,
            projectId,
            amount,
            address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSwap.Id, result.Value.Swap.Id);
        Assert.Equal(expectedSwap.LockupAddress, result.Value.Swap.LockupAddress);
        Assert.Equal(expectedSwap.BlindingKey, result.Value.Swap.BlindingKey);
    }

    [Fact]
    public async Task CreateSwap_WhenBoltzFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("test-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        SetupSuccessfulDependencies(projectId);

        _mockBoltzService
            .Setup(x => x.CalculateLiquidAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(101000L);

        _mockBoltzService
            .Setup(x => x.CreateLiquidToBtcSwapAsync(address, It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<BoltzSubmarineSwap>("Boltz API error"));

        var request = new CreateLiquidSwapForInvestment.CreateLiquidSwapRequest(
            walletId,
            projectId,
            amount,
            address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Boltz API error", result.Error);
    }

    [Fact]
    public async Task CreateSwap_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("non-existent-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        _mockBoltzService
            .Setup(x => x.CalculateLiquidAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(101000L);

        var request = new CreateLiquidSwapForInvestment.CreateLiquidSwapRequest(
            walletId,
            projectId,
            amount,
            address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Project not found", result.Error);
    }

    [Fact]
    public async Task CreateSwap_WhenCalculateAmountFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = new ProjectId("test-project");
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        SetupSuccessfulDependencies(projectId);

        _mockBoltzService
            .Setup(x => x.CalculateLiquidAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(Result.Failure<long>("Amount below minimum"));

        var request = new CreateLiquidSwapForInvestment.CreateLiquidSwapRequest(
            walletId,
            projectId,
            amount,
            address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Amount below minimum", result.Error);
    }
}


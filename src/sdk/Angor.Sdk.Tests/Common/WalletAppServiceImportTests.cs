using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.History;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DomainWallet = Angor.Sdk.Wallet.Domain.Wallet;

namespace Angor.Sdk.Tests.Common;

public class WalletAppServiceImportTests
{
    // Only passed to a mock factory; never used for real key derivation or wallet storage.
    private const string SeedWords = "mock-wallet-words";
    private static readonly WalletId WalletId = new("imported-wallet");
    private readonly Mock<IWalletFactory> _walletFactory = new(MockBehavior.Strict);
    private readonly Mock<IWalletAccountBalanceService> _balances = new(MockBehavior.Strict);
    private readonly Mock<ISeedwordsProvider> _seedwordsProvider = new(MockBehavior.Strict);

    [Fact]
    public async Task CreateWallet_WhenBalanceScanNeverCompletes_ReturnsWithoutStartingScan()
    {
        // Arrange: reproduce the network operation that previously held import open indefinitely.
        var scan = new TaskCompletionSource<Result<AccountBalanceInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _balances.Setup(x => x.RefreshAccountBalanceInfoAsync(WalletId)).Returns(scan.Task);
        SetupSuccessfulCreation();
        var sut = CreateSut();

        try
        {
            // Act
            var result = await sut.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
                BitcoinNetwork.Mainnet).WaitAsync(TimeSpan.FromSeconds(2));

            // Assert: no detached scan is left mutating a saved wallet after import succeeds.
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(WalletId);
            _walletFactory.VerifyAll();
            _balances.Verify(x => x.RefreshAccountBalanceInfoAsync(It.IsAny<WalletId>()), Times.Never);
        }
        finally
        {
            // Release the simulated hang even when running this regression against the old code.
            scan.TrySetResult(Result.Failure<AccountBalanceInfo>("Indexer unavailable"));
        }
    }

    [Fact]
    public async Task CreateWallet_WhenFactoryIsStillWorking_WaitsForLocalCreation()
    {
        // Arrange
        var creation = new TaskCompletionSource<Result<DomainWallet>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _walletFactory.Setup(x => x.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet)).Returns(creation.Task);
        var sut = CreateSut();

        // Act
        var import = sut.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None, BitcoinNetwork.Mainnet);
        bool completedBeforeCreation = import.IsCompleted;
        creation.SetResult(Result.Success(new DomainWallet(WalletId, null!)));
        var result = await import.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert: skipping sync must not report success before the wallet is actually saved.
        completedBeforeCreation.Should().BeFalse();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(WalletId);
        _balances.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateWallet_WhenFactoryFails_PreservesFailure()
    {
        // Arrange
        _walletFactory.Setup(x => x.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet)).ReturnsAsync(Result.Failure<DomainWallet>("Wallet could not be saved"));
        var sut = CreateSut();

        // Act
        var result = await sut.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Wallet could not be saved");
        _balances.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateWallet_WhenFactoryThrows_ReturnsReadableFailure()
    {
        // Arrange
        _walletFactory.Setup(x => x.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet)).ThrowsAsync(new InvalidOperationException("Secure storage unavailable"));
        var sut = CreateSut();

        // Act
        var result = await sut.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Failed to create wallet: Secure storage unavailable");
        _balances.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RestoreStoredWallet_WhenLocalCreationSucceeds_DoesNotStartBalanceScan()
    {
        // Arrange: stored-wallet restore shares the same creation path as a mnemonic import.
        _seedwordsProvider.Setup(x => x.GetSensitiveData(WalletId.Value))
            .ReturnsAsync(Result.Success((SeedWords, Maybe<string>.None)));
        SetupSuccessfulCreation();
        var sut = CreateSut();

        // Act
        var result = await sut.RestoreStoredWallet(WalletId.Value, "Imported wallet", BitcoinNetwork.Mainnet);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(WalletId);
        _walletFactory.VerifyAll();
        _seedwordsProvider.VerifyAll();
        _balances.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RefreshAndGetAccountBalanceInfo_AfterImport_StillRunsExplicitScan()
    {
        // Arrange
        SetupSuccessfulCreation();
        var scan = new TaskCompletionSource<Result<AccountBalanceInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _balances.Setup(x => x.RefreshAccountBalanceInfoAsync(WalletId)).Returns(scan.Task);
        var sut = CreateSut();

        try
        {
            var import = await sut.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
                BitcoinNetwork.Mainnet).WaitAsync(TimeSpan.FromSeconds(2));
            import.IsSuccess.Should().BeTrue();
            _balances.Verify(x => x.RefreshAccountBalanceInfoAsync(WalletId), Times.Never);

            // Act
            var refresh = sut.RefreshAndGetAccountBalanceInfo(WalletId);
            bool completedBeforeScan = refresh.IsCompleted;
            var balance = new AccountBalanceInfo();
            scan.SetResult(Result.Success(balance));
            var result = await refresh.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            completedBeforeScan.Should().BeFalse();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeSameAs(balance);
            _balances.Verify(x => x.RefreshAccountBalanceInfoAsync(WalletId), Times.Once);
        }
        finally
        {
            scan.TrySetResult(Result.Failure<AccountBalanceInfo>("Indexer unavailable"));
        }
    }

    private void SetupSuccessfulCreation()
    {
        _walletFactory.Setup(x => x.CreateWallet("Imported wallet", SeedWords, Maybe<string>.None,
            BitcoinNetwork.Mainnet)).ReturnsAsync(Result.Success(new DomainWallet(WalletId, null!)));
    }

    private WalletAppService CreateSut()
    {
        return new WalletAppService(
            Mock.Of<ISensitiveWalletDataProvider>(),
            _walletFactory.Object,
            Mock.Of<IWalletOperations>(),
            Mock.Of<IWalletStore>(),
            Mock.Of<IPsbtOperations>(),
            Mock.Of<ITransactionHistory>(),
            Mock.Of<IHttpClientFactory>(),
            _balances.Object,
            Mock.Of<IGenericDocumentCollection<DerivedProjectKeys>>(),
            Mock.Of<IGenericDocumentCollection<FounderProjectsDocument>>(),
            Mock.Of<IGenericDocumentCollection<InvestmentRecordsDocument>>(),
            Mock.Of<IGenericDocumentCollection<InvestmentHandshake>>(),
            Mock.Of<ISecureKeyProvider>(),
            _seedwordsProvider.Object,
            NullLogger<WalletAppService>.Instance);
    }
}

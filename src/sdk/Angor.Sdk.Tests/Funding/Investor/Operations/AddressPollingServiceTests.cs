using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for AddressPollingService (IAddressPollingService).
/// Tests the core polling loop behavior that was previously inside MempoolMonitoringService.MonitorAddressForFundsAsync.
/// These tests lock down the behavior before the refactor so we can prove no regression.
/// </summary>
public class AddressPollingServiceTests
{
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly AddressPollingService _sut;

    public AddressPollingServiceTests()
    {
        _mockIndexerService = new Mock<IIndexerService>();
        _sut = new AddressPollingService(
            _mockIndexerService.Object,
            new NullLogger<AddressPollingService>());
    }

    [Fact]
    public async Task WaitForFunds_WhenSufficientFundsOnFirstPoll_ReturnsUtxosImmediately()
    {
        // Arrange
        var address = "tb1qtest_immediate";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 150_000, "txid1", 0)
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(150_000, result[0].value);

        _mockIndexerService.Verify(
            x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task WaitForFunds_WhenFundsArriveOnSecondPoll_RetriesAndReturns()
    {
        // Arrange
        var address = "tb1qtest_retry";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 120_000, "txid1", 0)
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call returns empty, second call returns funds
                return callCount >= 2 ? utxos : new List<UtxoData>();
            });

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(120_000, result[0].value);
        Assert.True(callCount >= 2, "Should have polled at least twice");
    }

    [Fact]
    public async Task WaitForFunds_WhenCancelled_ReturnsEmptyList()
    {
        // Arrange
        var address = "tb1qtest_cancel";
        var requiredSats = 100_000L;
        var cts = new CancellationTokenSource();

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                // Cancel after first poll returns no funds
                cts.Cancel();
                return new List<UtxoData>();
            });

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(50),
            cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task WaitForFunds_WhenTimeout_ReturnsEmptyList()
    {
        // Arrange
        var address = "tb1qtest_timeout";
        var requiredSats = 100_000L;

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<UtxoData>());

        // Act - use a very short timeout
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(30),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task WaitForFunds_WithMultipleUtxos_SumsCorrectly()
    {
        // Arrange
        var address = "tb1qtest_multi";
        var requiredSats = 200_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 80_000, "txid1", 0),
            CreateUtxo(address, 70_000, "txid2", 0),
            CreateUtxo(address, 60_000, "txid3", 0)
            // Total: 210_000 >= 200_000
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(210_000, result.Sum(u => u.value));
    }

    [Fact]
    public async Task WaitForFunds_WithInsufficientMultipleUtxos_ContinuesPolling()
    {
        // Arrange
        var address = "tb1qtest_insufficient";
        var requiredSats = 200_000L;
        var insufficientUtxos = new List<UtxoData>
        {
            CreateUtxo(address, 50_000, "txid1", 0),
            CreateUtxo(address, 30_000, "txid2", 0)
            // Total: 80_000 < 200_000
        };
        var sufficientUtxos = new List<UtxoData>
        {
            CreateUtxo(address, 50_000, "txid1", 0),
            CreateUtxo(address, 30_000, "txid2", 0),
            CreateUtxo(address, 150_000, "txid3", 0)
            // Total: 230_000 >= 200_000
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount >= 3 ? sufficientUtxos : insufficientUtxos;
            });

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(230_000, result.Sum(u => u.value));
        Assert.True(callCount >= 3);
    }

    [Fact]
    public async Task WaitForFunds_OnlyCountsMempoolUtxos_IgnoresConfirmed()
    {
        // Arrange
        var address = "tb1qtest_mempool_only";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 90_000, "txid1", 0, blockIndex: 500), // Confirmed - should be ignored
            CreateUtxo(address, 110_000, "txid2", 0, blockIndex: 0)   // Mempool - should be counted
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(110_000, result[0].value);
        Assert.Equal(0, result[0].blockIndex); // Only mempool UTXOs returned
    }

    [Fact]
    public async Task WaitForFunds_WhenIndexerThrows_ContinuesPolling()
    {
        // Arrange
        var address = "tb1qtest_error_recovery";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 150_000, "txid1", 0)
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Network error");
                return utxos;
            });

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(callCount >= 2, "Should have retried after error");
    }

    [Fact]
    public async Task WaitForFunds_WhenIndexerReturnsNull_ContinuesPolling()
    {
        // Arrange
        var address = "tb1qtest_null";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 150_000, "txid1", 0)
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return null;
                return utxos;
            });

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task WaitForFunds_ExactAmountMatch_ReturnsUtxos()
    {
        // Arrange
        var address = "tb1qtest_exact";
        var requiredSats = 100_000L;
        var utxos = new List<UtxoData>
        {
            CreateUtxo(address, 100_000, "txid1", 0) // Exactly the required amount
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.WaitForFundsAsync(
            address, requiredSats,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(100_000, result[0].value);
    }

    #region Helper Methods

    private static UtxoData CreateUtxo(string address, long value, string txId, int outputIndex, int blockIndex = 0)
    {
        return new UtxoData
        {
            address = address,
            value = value,
            outpoint = new Outpoint(txId, outputIndex),
            scriptHex = "",
            blockIndex = blockIndex
        };
    }

    #endregion
}


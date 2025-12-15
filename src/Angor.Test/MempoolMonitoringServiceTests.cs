using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Test;

public class MempoolMonitoringServiceTests
{
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly MempoolMonitoringService _sut;

    public MempoolMonitoringServiceTests()
    {
        _mockIndexerService = new Mock<IIndexerService>();
        _sut = new MempoolMonitoringService(
            _mockIndexerService.Object,
            new NullLogger<MempoolMonitoringService>());
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WhenFundsDetected_ReturnsUtxos()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L; // 1 BTC
        var timeout = TimeSpan.FromMinutes(1);

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 150000000, // 1.5 BTC
                blockIndex = 0 // Unconfirmed (in mempool)
            }
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(150000000, result[0].value);
        Assert.Equal(0, result[0].blockIndex); // Should be mempool transaction
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WhenMultipleUtxosAggregate_ReturnsAll()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 200000000L; // 2 BTC
        var timeout = TimeSpan.FromMinutes(1);

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 100000000, // 1 BTC
                blockIndex = 0
            },
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 1),
                value = 150000000, // 1.5 BTC
                blockIndex = 0
            }
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(250000000, result.Sum(u => u.value)); // Total 2.5 BTC
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WhenNoFunds_ThrowsTimeoutException()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromSeconds(5); // Short timeout for test

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(new List<UtxoData>()); // No UTXOs

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            _sut.MonitorAddressForFundsAsync(
                address,
                requiredAmount,
                timeout,
                CancellationToken.None));
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WhenPartialFunds_ContinuesMonitoring()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 200000000L; // 2 BTC
        var timeout = TimeSpan.FromSeconds(15);

        var partialUtxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 100000000, // Only 1 BTC
                blockIndex = 0
            }
        };

        var fullUtxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 100000000,
                blockIndex = 0
            },
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 1),
                value = 150000000, // Now total is 2.5 BTC
                blockIndex = 0
            }
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? partialUtxos : fullUtxos;
            });

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(callCount >= 2, "Should have polled multiple times");
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromMinutes(1);
        var cts = new CancellationTokenSource();

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(() =>
            {
                cts.Cancel(); // Cancel during first call
                return new List<UtxoData>();
            });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.MonitorAddressForFundsAsync(
                address,
                requiredAmount,
                timeout,
                cts.Token));
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_IgnoresConfirmedUtxos()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromMinutes(1);

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 150000000,
                blockIndex = 700000 // Confirmed transaction
            }
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(utxos);

        // Act & Assert
        // Should timeout because confirmed UTXOs are ignored
        await Assert.ThrowsAsync<TimeoutException>(() =>
            _sut.MonitorAddressForFundsAsync(
                address,
                requiredAmount,
                TimeSpan.FromSeconds(5), // Short timeout
                CancellationToken.None));
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_HandlesIndexerErrors_ContinuesMonitoring()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromSeconds(15);

        var validUtxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 150000000,
                blockIndex = 0
            }
        };

        var callCount = 0;
        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Indexer temporary error");
                return validUtxos;
            });

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(callCount >= 2, "Should have retried after error");
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WithExactAmount_ReturnsImmediately()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromMinutes(1);

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 100000000, // Exactly required amount
                blockIndex = 0
            }
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(utxos);

        var startTime = DateTime.UtcNow;

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(elapsed.TotalSeconds < 5, "Should return quickly when funds are available");
    }

    [Fact]
    public async Task MonitorAddressForFundsAsync_WithMixedConfirmedAndUnconfirmed_ReturnsOnlyUnconfirmed()
    {
        // Arrange
        var address = "bc1qtest123";
        var requiredAmount = 100000000L;
        var timeout = TimeSpan.FromMinutes(1);

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 50000000,
                blockIndex = 700000 // Confirmed - should be ignored
            },
            new UtxoData
            {
                address = address,
                scriptHex = "0014test",
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 1),
                value = 150000000,
                blockIndex = 0 // Unconfirmed - should be included
            }
        };

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, 50, 0))
            .ReturnsAsync(utxos);

        // Act
        var result = await _sut.MonitorAddressForFundsAsync(
            address,
            requiredAmount,
            timeout,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(0, result[0].blockIndex);
        Assert.Equal(150000000, result[0].value);
    }
}


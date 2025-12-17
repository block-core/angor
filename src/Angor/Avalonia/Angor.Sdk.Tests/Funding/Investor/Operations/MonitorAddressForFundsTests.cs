using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for MonitorAddressForFunds handler.
/// Tests the address monitoring functionality for detecting incoming funds.
/// </summary>
public class MonitorAddressForFundsTests
{
    private readonly Mock<IMempoolMonitoringService> _mockMempoolMonitoringService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly MonitorAddressForFunds.MonitorAddressForFundsHandler _sut;
    private readonly INetworkConfiguration _networkConfiguration;

    public MonitorAddressForFundsTests()
    {
        _mockMempoolMonitoringService = new Mock<IMempoolMonitoringService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        
        _networkConfiguration = new NetworkConfiguration();
        _networkConfiguration.SetNetwork(Angor.Shared.Networks.Networks.Bitcoin.Testnet());

        _sut = new MonitorAddressForFunds.MonitorAddressForFundsHandler(
            _mockMempoolMonitoringService.Object,
            _mockWalletAccountBalanceService.Object,
            new NullLogger<MonitorAddressForFunds.MonitorAddressForFundsHandler>());
    }

    [Fact]
    public async Task Handle_WhenFundsDetected_ReturnsSuccessWithUtxos()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtest123address";
        var requiredAmount = new Amount(100000); // 0.001 BTC
        
        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var expectedUtxos = new List<UtxoData>
        {
            CreateUtxoData(address, 150000, "txid1", 0)
        };

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUtxos);

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, $"Expected success but got: {(result.IsFailure ? result.Error : "N/A")}");
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.DetectedUtxos);
        Assert.Equal(150000, result.Value.TotalAmount.Sats);
        Assert.Equal(address, result.Value.Address);
    }

    [Fact]
    public async Task Handle_WhenMultipleUtxosDetected_ReturnsAllUtxos()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtest456address";
        var requiredAmount = new Amount(200000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var expectedUtxos = new List<UtxoData>
        {
            CreateUtxoData(address, 100000, "txid1", 0),
            CreateUtxoData(address, 80000, "txid2", 0),
            CreateUtxoData(address, 50000, "txid3", 0)
        };

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUtxos);

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.DetectedUtxos.Count);
        Assert.Equal(230000, result.Value.TotalAmount.Sats);
    }

    [Fact]
    public async Task Handle_WhenNoFundsDetected_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtest789address";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UtxoData>());

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("No funds detected", result.Error);
    }

    [Fact]
    public async Task Handle_WhenAddressNotInWallet_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var unknownAddress = "tb1qunknownaddress";
        var requiredAmount = new Amount(100000);

        // Create account info without the requested address
        var accountInfo = CreateAccountInfoWithAddress("tb1qdifferentaddress");
        SetupAccountBalanceMock(accountInfo);

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            unknownAddress,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found in wallet", result.Error);
    }

    [Fact]
    public async Task Handle_WhenAccountBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestaddress";
        var requiredAmount = new Amount(100000);

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Failed to get account balance"));

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Failed to get account balance", result.Error);
    }

    [Fact]
    public async Task Handle_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestcanceladdress";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var cts = new CancellationTokenSource();

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, cts.Token);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("cancelled", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTimeout_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtesttimeoutaddress";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Monitoring timed out"));

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("timed out", result.Error);
    }

    [Fact]
    public async Task Handle_WhenExactAmountDetected_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestexactaddress";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var expectedUtxos = new List<UtxoData>
        {
            CreateUtxoData(address, 100000, "txid1", 0) // Exact amount
        };

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUtxos);

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100000, result.Value.TotalAmount.Sats);
    }

    [Fact]
    public async Task Handle_UsesDefaultTimeoutWhenNotProvided()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestdefaulttimeout";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var expectedUtxos = new List<UtxoData>
        {
            CreateUtxoData(address, 100000, "txid1", 0)
        };

        TimeSpan capturedTimeout = TimeSpan.Zero;
        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, long, TimeSpan, CancellationToken>((_, _, timeout, _) => capturedTimeout = timeout)
            .ReturnsAsync(expectedUtxos);

        // Request without timeout - should use default
        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            null); // No timeout provided

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMinutes(30), capturedTimeout); // Default timeout
    }

    [Fact]
    public async Task Handle_WhenMempoolServiceThrowsGenericException_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestexceptionaddress";
        var requiredAmount = new Amount(100000);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            TimeSpan.FromMinutes(5));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Error monitoring address", result.Error);
    }

    [Fact]
    public async Task Handle_VerifiesMonitoringServiceCalledWithCorrectParameters()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var address = "tb1qtestparamsaddress";
        var requiredAmount = new Amount(250000);
        var timeout = TimeSpan.FromMinutes(10);

        var accountInfo = CreateAccountInfoWithAddress(address);
        SetupAccountBalanceMock(accountInfo);

        var expectedUtxos = new List<UtxoData>
        {
            CreateUtxoData(address, 300000, "txid1", 0)
        };

        _mockMempoolMonitoringService
            .Setup(x => x.MonitorAddressForFundsAsync(
                address,
                requiredAmount.Sats,
                timeout,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUtxos);

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            address,
            requiredAmount,
            timeout);

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockMempoolMonitoringService.Verify(
            x => x.MonitorAddressForFundsAsync(
                address,
                250000,
                TimeSpan.FromMinutes(10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #region Helper Methods

    private AccountInfo CreateAccountInfoWithAddress(string address)
    {
        var accountInfo = new AccountInfo
        {
            walletId = Guid.NewGuid().ToString(),
            LastFetchIndex = 0,
            AddressesInfo = new List<AddressInfo>
            {
                new AddressInfo
                {
                    Address = address,
                    HdPath = "m/84'/0'/0'/0/0",
                    UtxoData = new List<UtxoData>()
                }
            },
            ChangeAddressesInfo = new List<AddressInfo>()
        };

        return accountInfo;
    }

    private void SetupAccountBalanceMock(AccountInfo accountInfo)
    {
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalanceInfo));
    }

    private UtxoData CreateUtxoData(string address, long value, string txId, int outputIndex)
    {
        return new UtxoData
        {
            address = address,
            value = value,
            outpoint = new Outpoint(txId, outputIndex),
            scriptHex = "", // Empty for test purposes
            blockIndex = 0 // 0 = mempool transaction
        };
    }

    #endregion
}


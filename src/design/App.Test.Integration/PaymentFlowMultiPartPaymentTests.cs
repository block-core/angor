using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Application;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Models;
using App.UI.Shared;
using App.UI.Shared.PaymentFlow;
using App.UI.Shared.Services;
using App.UI.Shell;
using Avalonia.Headless.XUnit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace App.Test.Integration;

public class PaymentFlowMultiPartPaymentTests
{
    private sealed class EmptyStore : IStore
    {
        public Task<Result> Save<T>(string key, T data) => Task.FromResult(Result.Success());

        public Task<Result<T>> Load<T>(string key) => Task.FromResult(Result.Failure<T>("nothing stored"));

        public Task<Result> Delete(string key) => Task.FromResult(Result.Success());
    }

    private readonly Mock<IInvestmentAppService> _investmentAppService = new();
    private readonly Mock<IWalletContext> _walletContext = new();
    private readonly WalletInfo _wallet = new(new WalletId("wallet-1"), "Wallet", "TBTC");
    private int _callbackCount;

    private PaymentFlowViewModel CreateViewModel()
    {
        var wallets = new ReadOnlyObservableCollection<WalletInfo>(new ObservableCollection<WalletInfo> { _wallet });
        _walletContext.Setup(x => x.Wallets).Returns(wallets);
        _walletContext.Setup(x => x.WalletsUpdated).Returns(Observable.Never<Unit>());
        _walletContext.Setup(x => x.RefreshBalanceAsync(It.IsAny<WalletId>())).Returns(Task.CompletedTask);

        var currency = new Mock<ICurrencyService>();
        currency.Setup(x => x.Symbol).Returns("TBTC");

        var config = new PaymentFlowConfig
        {
            AmountSats = 100_000,
            Title = "Invest",
            SuccessTitle = "Done",
            SuccessButtonText = "Close",
            OnSuccessButtonClicked = () => { },
            OnPaymentReceived = (_, _, _) =>
            {
                _callbackCount++;
                return Task.FromResult(Result.Success());
            },
        };

        var boltz = new Mock<IBoltzSwapService>();
        var vm = new PaymentFlowViewModel(
            Mock.Of<IWalletAppService>(),
            _investmentAppService.Object,
            boltz.Object,
            Mock.Of<IBoltzSwapStorageService>(),
            _walletContext.Object,
            currency.Object,
            () => default!,
            NullLogger.Instance,
            new PrototypeSettings(new EmptyStore(), NullLogger<PrototypeSettings>.Instance),
            config);
        vm.OnChainAddress = "tb1qtestaddress";
        return vm;
    }

    private static MonitorAddressForFunds.MonitorAddressForFundsResponse Response(int utxoCount)
    {
        var utxos = Enumerable.Range(0, utxoCount).Select(_ => new UtxoData { value = 50_000 }).ToList();
        return new MonitorAddressForFunds.MonitorAddressForFundsResponse(
            utxos, new Amount(utxos.Sum(u => u.value)), "tb1qtestaddress");
    }

    [AvaloniaFact]
    public async Task PayToOnChainAddress_WhenSingleUtxoDetected_RunsPostPaymentCallback()
    {
        // Arrange
        _investmentAppService
            .Setup(x => x.MonitorAddressForFunds(It.IsAny<MonitorAddressForFunds.MonitorAddressForFundsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Response(1)));
        var vm = CreateViewModel();

        // Act
        await vm.PayToOnChainAddressCommand.Execute();

        // Assert
        _callbackCount.Should().Be(1);
        vm.CurrentScreen.Should().NotBe(PaymentFlowScreen.PaidInParts);
    }

    [AvaloniaFact]
    public async Task PayToOnChainAddress_WhenMultipleUtxosDetected_RedirectsToPayFromWallet()
    {
        // Arrange
        _investmentAppService
            .Setup(x => x.MonitorAddressForFunds(It.IsAny<MonitorAddressForFunds.MonitorAddressForFundsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Response(2)));
        var vm = CreateViewModel();

        // Act
        await vm.PayToOnChainAddressCommand.Execute();

        // Assert
        _callbackCount.Should().Be(0);
        vm.CurrentScreen.Should().Be(PaymentFlowScreen.PaidInParts);
        vm.PaidInPartsMessage.Should().Contain("multiple parts").And.NotContain("UTXO");
        _walletContext.Verify(x => x.RefreshBalanceAsync(_wallet.Id), Times.AtLeastOnce);

        vm.ReturnToWalletSelector();
        vm.CurrentScreen.Should().Be(PaymentFlowScreen.WalletSelector);
    }

    [AvaloniaFact]
    public async Task PayToOnChainAddress_WhenMonitorTimesOutButBalanceIncreased_RedirectsToPayFromWallet()
    {
        // Arrange
        _investmentAppService
            .Setup(x => x.MonitorAddressForFunds(It.IsAny<MonitorAddressForFunds.MonitorAddressForFundsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<MonitorAddressForFunds.MonitorAddressForFundsResponse>("timeout"));
        var vm = CreateViewModel();
        _walletContext.Setup(x => x.RefreshBalanceAsync(It.IsAny<WalletId>()))
            .Callback(() => _wallet.TotalBalanceSats = 60_000)
            .Returns(Task.CompletedTask);

        // Act
        await vm.PayToOnChainAddressCommand.Execute();

        // Assert
        _callbackCount.Should().Be(0);
        vm.CurrentScreen.Should().Be(PaymentFlowScreen.PaidInParts);
        vm.HasError.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task PayToOnChainAddress_WhenMonitorFailsAndBalanceUnchanged_ShowsError()
    {
        // Arrange
        _investmentAppService
            .Setup(x => x.MonitorAddressForFunds(It.IsAny<MonitorAddressForFunds.MonitorAddressForFundsRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<MonitorAddressForFunds.MonitorAddressForFundsResponse>("timeout"));
        var vm = CreateViewModel();

        // Act
        await vm.PayToOnChainAddressCommand.Execute();

        // Assert
        vm.CurrentScreen.Should().NotBe(PaymentFlowScreen.PaidInParts);
        vm.HasError.Should().BeTrue();
    }
}
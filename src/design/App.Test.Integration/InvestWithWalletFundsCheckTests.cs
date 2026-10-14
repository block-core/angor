using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Application;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Services;
using App.UI.Shell;
using Avalonia.Headless.XUnit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace App.Test.Integration;

public class InvestWithWalletFundsCheckTests
{
    /// <summary>Persistence stub that reports nothing stored, so PrototypeSettings keeps its defaults.</summary>
    private sealed class EmptyStore : IStore
    {
        public Task<Result> Save<T>(string key, T data) => Task.FromResult(Result.Success());

        public Task<Result<T>> Load<T>(string key) => Task.FromResult(Result.Failure<T>("nothing stored"));

        public Task<Result> Delete(string key) => Task.FromResult(Result.Success());
    }

    private readonly Mock<IInvestmentAppService> _investmentAppService = new();
    private readonly Mock<IWalletContext> _walletContext = new();
    private readonly WalletId _walletId = new("wallet-1");

    private InvestPageViewModel CreateViewModel(WalletInfo wallet)
    {
        var wallets = new ReadOnlyObservableCollection<WalletInfo>(new ObservableCollection<WalletInfo> { wallet });
        _walletContext.Setup(x => x.Wallets).Returns(wallets);
        _walletContext.Setup(x => x.RefreshAllBalancesAsync()).Returns(Task.CompletedTask);
        _walletContext.Setup(x => x.WalletsUpdated).Returns(Observable.Never<Unit>());

        var currency = new Mock<ICurrencyService>();
        currency.Setup(x => x.Symbol).Returns("TBTC");

        var portfolio = new PortfolioViewModel(
            _investmentAppService.Object,
            Mock.Of<Angor.Sdk.Funding.Projects.IProjectAppService>(),
            _walletContext.Object,
            new SignatureStore(currency.Object),
            currency.Object,
            NullLogger<PortfolioViewModel>.Instance);

        return new InvestPageViewModel(
            new ProjectItemViewModel { ProjectId = "project-1", ProjectName = "Test" },
            Mock.Of<IWalletAppService>(),
            _investmentAppService.Object,
            Mock.Of<Angor.Shared.Integration.Lightning.IBoltzSwapService>(),
            Mock.Of<Angor.Shared.Integration.Lightning.IBoltzSwapStorageService>(),
            portfolio,
            currency.Object,
            _walletContext.Object,
            () => default,
            new PrototypeSettings(new EmptyStore(), NullLogger<PrototypeSettings>.Instance),
            NullLogger<InvestPageViewModel>.Instance);
    }

    private void VerifyNoInvestmentWasBuilt()
    {
        _investmentAppService.Verify(
            x => x.BuildInvestmentDraft(It.IsAny<BuildInvestmentDraft.BuildInvestmentDraftRequest>()), Times.Never);
        _investmentAppService.Verify(
            x => x.SubmitInvestment(It.IsAny<RequestInvestmentSignatures.RequestFounderSignaturesRequest>()), Times.Never);
    }

    [AvaloniaFact]
    public async Task InvestWithWallet_WhenAvailableBalanceBelowAmount_FailsWithoutCallingInvestmentService()
    {
        // Arrange
        var wallet = new WalletInfo(_walletId, "Wallet", "TBTC") { TotalBalanceSats = 5_000 };
        InvestPageViewModel viewModel = CreateViewModel(wallet);

        // Act
        Result result = await viewModel.InvestWithWalletAsync(_walletId, 10_000, 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Insufficient funds in the selected wallet.");
        VerifyNoInvestmentWasBuilt();
    }

    [AvaloniaFact]
    public async Task InvestWithWallet_WhenOnlyConfirmedPlusUnconfirmedBelowAmount_Fails()
    {
        // Arrange - AvailableSats = confirmed + unconfirmed = 9_999 < 10_000
        var wallet = new WalletInfo(_walletId, "Wallet", "TBTC")
        {
            TotalBalanceSats = 6_000,
            UnconfirmedBalanceSats = 3_999
        };
        InvestPageViewModel viewModel = CreateViewModel(wallet);

        // Act
        Result result = await viewModel.InvestWithWalletAsync(_walletId, 10_000, 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        VerifyNoInvestmentWasBuilt();
    }

    [AvaloniaFact]
    public async Task InvestWithWallet_WhenUnconfirmedFundsCoverAmount_ProceedsToBuildDraft()
    {
        // Arrange - confirmed alone is not enough, but AvailableSats (incl. unconfirmed) is.
        var wallet = new WalletInfo(_walletId, "Wallet", "TBTC")
        {
            TotalBalanceSats = 4_000,
            UnconfirmedBalanceSats = 6_000
        };
        InvestPageViewModel viewModel = CreateViewModel(wallet);

        _investmentAppService
            .Setup(x => x.BuildInvestmentDraft(It.IsAny<BuildInvestmentDraft.BuildInvestmentDraftRequest>()))
            .ReturnsAsync(Result.Failure<BuildInvestmentDraft.BuildInvestmentDraftResponse>("stop here"));

        // Act
        Result result = await viewModel.InvestWithWalletAsync(_walletId, 10_000, 20);

        // Assert - the funds check passed, so the build step was reached.
        result.Error.Should().Be("stop here");
        _investmentAppService.Verify(
            x => x.BuildInvestmentDraft(It.IsAny<BuildInvestmentDraft.BuildInvestmentDraftRequest>()), Times.Once);
    }
}
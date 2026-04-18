using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Wallet.Application;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Test.Integration;

/// <summary>
/// Full headless integration test for the 1-click invest on-chain invoice flow.
/// No mocks — uses the real DI container and SDK wiring.
///
/// Exercises the complete state machine:
///   InvestForm → set amount → ShowInvoice → on-chain address generation →
///   monitoring starts → (timeout/error expected without testnet) →
///   tab switching clears on-chain error (bug fix verification) →
///   CloseModal resets everything.
/// </summary>
public class OneClickInvestOnChainTest
{
    [AvaloniaFact]
    public async Task OnChainInvoiceFlow_FullStateMachine()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestOnChainTest));
        Log("========== STARTING 1-click invest ON-CHAIN test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var walletAppService = services.GetRequiredService<IWalletAppService>();
        var investmentAppService = services.GetRequiredService<IInvestmentAppService>();
        var portfolioVm = services.GetRequiredService<PortfolioViewModel>();
        var currencyService = services.GetRequiredService<ICurrencyService>();
        var walletContext = services.GetRequiredService<IWalletContext>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<InvestPageViewModel>();

        var project = CreateTestProject("Fund");

        var vm = new InvestPageViewModel(
            project,
            walletAppService,
            investmentAppService,
            portfolioVm,
            currencyService,
            walletContext,
            logger);

        // ── Step 1: Initial state ──
        Log("[1] Verify initial form state...");
        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        vm.IsProcessing.Should().BeFalse();
        vm.HasError.Should().BeFalse();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);

        // ── Step 2: Set investment amount ──
        Log("[2] Set investment amount...");
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");
        vm.FormattedAmount.Should().Be("0.00100000");

        // ── Step 3: ShowInvoice transitions to Invoice screen, starts on-chain flow ──
        Log("[3] ShowInvoice — on-chain default...");
        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "on-chain is the default tab");
        vm.IsProcessing.Should().BeTrue("monitoring starts synchronously");
        vm.PaymentStatusText.Should().Be("Generating invoice address...",
            "immediate feedback before any async work completes");
        vm.InvoiceString.Should().Be(vm.PaymentStatusText,
            "InvoiceString follows PaymentStatusText while address is still loading");

        // Derived tab visibility
        vm.IsOnChainTab.Should().BeTrue();
        vm.IsLightningTab.Should().BeFalse();
        vm.InvoiceFieldLabel.Should().Be("On-Chain Address");
        vm.InvoiceTabIcon.Should().Contain("bitcoin");

        // ── Step 4: Let the async on-chain flow run — expect error (no wallet/no testnet) ──
        Log("[4] Pumping UI to let on-chain flow complete...");
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.OnChainAddress != null,
            TimeSpan.FromSeconds(10));

        Log($"    OnChainAddress: '{vm.OnChainAddress}'");
        Log($"    ErrorMessage: '{vm.ErrorMessage}'");
        Log($"    PaymentStatusText: '{vm.PaymentStatusText}'");

        // Without a funded testnet wallet we expect a labeled error
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("Parameter 'key'",
                "raw ArgumentNullException must not leak — defensive pre-checks should catch it");
            vm.HasError.Should().BeTrue("HasError must reflect the ErrorMessage state");
        }

        // ── Step 5: Switch to Lightning — on-chain error must clear (bug fix) ──
        Log("[5] Switch to Lightning tab — error must clear...");
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        vm.IsLightningTab.Should().BeTrue();
        vm.IsOnChainTab.Should().BeFalse();
        vm.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        vm.InvoiceTabIcon.Should().Contain("bolt");

        // The critical bug fix: on-chain monitoring error must not bleed through.
        // The Lightning path may set its own error (e.g. "No wallet available for Lightning swap")
        // which is fine — what matters is the on-chain monitoring error is gone.
        // Give a moment for any racing cancelled-monitor result to arrive.
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.LightningInvoice != null,
            TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();

        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("monitoring",
                "cancelled on-chain monitoring errors must not leak into the Lightning tab — " +
                "any error here must come from the Lightning path itself");
        }

        // ── Step 6: Switch back to on-chain — fresh flow starts ──
        Log("[6] Switch back to OnChain tab...");
        vm.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        vm.IsOnChainTab.Should().BeTrue();
        // The on-chain flow starts but may complete instantly (no wallet → immediate error),
        // so we only verify the tab switched correctly.

        // ── Step 7: CloseModal resets everything ──
        Log("[7] CloseModal resets all state...");
        vm.CloseModal();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        vm.IsProcessing.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.OnChainAddress.Should().BeNull();
        vm.LightningInvoice.Should().BeNull();
        vm.LightningSwapId.Should().BeNull();
        vm.IsGeneratingLightningInvoice.Should().BeFalse();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        vm.PaymentReceived.Should().BeFalse();

        window.Close();
        Log("========== 1-click invest ON-CHAIN test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task OnChainInvoiceFlow_TabSwitchRaceCondition()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestOnChainTest) + "_Race");
        Log("========== STARTING tab-switch race condition test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var vm = new InvestPageViewModel(
            CreateTestProject("Fund"),
            services.GetRequiredService<IWalletAppService>(),
            services.GetRequiredService<IInvestmentAppService>(),
            services.GetRequiredService<PortfolioViewModel>(),
            services.GetRequiredService<ICurrencyService>(),
            services.GetRequiredService<IWalletContext>(),
            services.GetRequiredService<ILoggerFactory>().CreateLogger<InvestPageViewModel>());

        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();

        // Start on-chain monitoring
        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        // Rapidly switch tabs — simulates a user clicking around
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        vm.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        // Wait for any cancelled operations to settle
        await PumpUntilAsync(
            () => !vm.IsProcessing || !string.IsNullOrEmpty(vm.ErrorMessage) || vm.LightningInvoice != null,
            TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();

        // After rapid switching, we should be on Lightning with no on-chain error bleeding through
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        if (vm.ErrorMessage != null)
        {
            // Any error should be from the Lightning path, not stale on-chain monitoring
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "stale on-chain monitoring errors must not leak after tab switch");
            vm.ErrorMessage.Should().NotContain("monitoring was cancelled",
                "cancelled on-chain operations must not surface as user-facing errors");
        }

        window.Close();
        Log("========== Tab-switch race condition test PASSED ==========");
    }

    private static ProjectItemViewModel CreateTestProject(string projectType) => new()
    {
        ProjectId = "headless-onchain-test-project",
        ProjectName = "Headless OnChain Test",
        ProjectType = projectType,
        FounderKey = "00",
        NostrNpub = "00",
        Target = "1.0",
        TargetLabel = "1 BTC",
        InvestorLabel = "0",
        Status = "Open",
        StartDate = DateTime.UtcNow.ToString("dd MMM yyyy"),
        EndDate = DateTime.UtcNow.AddDays(30).ToString("dd MMM yyyy"),
        PenaltyDays = "30",
        PenaltyThresholdSats = 1_000_000
    };

    private static async Task PumpUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

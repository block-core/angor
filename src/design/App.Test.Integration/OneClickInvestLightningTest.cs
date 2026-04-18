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
/// Full headless integration test for the 1-click invest Lightning invoice flow.
/// No mocks — uses the real DI container and SDK wiring.
///
/// Exercises the Lightning-specific state machine:
///   InvestForm → set amount → ShowInvoice → switch to Lightning tab →
///   Boltz swap creation → invoice display → monitoring →
///   (timeout/error expected without testnet) →
///   verify error is from Lightning path (not stale on-chain) →
///   portfolio deduplication after investment completion.
/// </summary>
public class OneClickInvestLightningTest
{
    [AvaloniaFact]
    public async Task LightningInvoiceFlow_FullStateMachine()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestLightningTest));
        Log("========== STARTING 1-click invest LIGHTNING test ==========");

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

        // ── Step 1: Set amount and enter invoice screen ──
        Log("[1] Set amount and show invoice...");
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue();

        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();
        vm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "ShowInvoice defaults to on-chain");

        // ── Step 2: Switch to Lightning tab ──
        Log("[2] Switch to Lightning tab...");
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        vm.IsLightningTab.Should().BeTrue();
        vm.IsOnChainTab.Should().BeFalse();
        vm.IsProcessing.Should().BeTrue("Lightning flow starts synchronously");
        vm.IsGeneratingLightningInvoice.Should().BeTrue(
            "spinner flag must flip immediately on Lightning tap");
        vm.PaymentStatusText.Should().Be("Creating Lightning invoice...",
            "synchronous status text gives immediate feedback");
        vm.InvoiceString.Should().Be("Creating Lightning invoice...",
            "InvoiceString follows PaymentStatusText while invoice is loading");
        vm.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        vm.InvoiceTabIcon.Should().Contain("bolt");

        // On-chain state must be cleared
        vm.OnChainAddress.Should().BeNull("on-chain address cleared on tab switch");
        vm.LightningInvoice.Should().BeNull("invoice not yet generated");

        // ── Step 3: Let Lightning flow run — expect error or invoice ──
        Log("[3] Pumping UI to let Lightning flow complete...");
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) ||
                  !string.IsNullOrEmpty(vm.LightningInvoice) ||
                  !vm.IsGeneratingLightningInvoice,
            TimeSpan.FromSeconds(15));

        Log($"    LightningInvoice: '{vm.LightningInvoice}'");
        Log($"    LightningSwapId: '{vm.LightningSwapId}'");
        Log($"    ErrorMessage: '{vm.ErrorMessage}'");
        Log($"    IsGeneratingLightningInvoice: {vm.IsGeneratingLightningInvoice}");

        if (vm.LightningInvoice != null)
        {
            // Boltz swap was created successfully
            vm.LightningSwapId.Should().NotBeNullOrEmpty("swap ID must be set when invoice is available");
            vm.IsGeneratingLightningInvoice.Should().BeFalse("spinner clears after invoice generation");
            vm.InvoiceString.Should().Be(vm.LightningInvoice,
                "InvoiceString shows the actual BOLT11 invoice once available");
        }
        else if (vm.ErrorMessage != null)
        {
            // Without a testnet wallet we expect a labeled error
            vm.ErrorMessage.Should().NotContain("Parameter 'key'",
                "raw ArgumentNullException must not leak to user");
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "on-chain monitoring error must not appear on the Lightning tab");
            vm.HasError.Should().BeTrue();
        }

        // ── Step 4: Verify on-chain error does NOT bleed into Lightning tab ──
        Log("[4] Verify no on-chain monitoring error bleeds through...");
        // The specific bug: when switching from on-chain to Lightning, the cancelled
        // on-chain monitoring could return a failure result ("monitoring has stopped")
        // which overwrites the cleared ErrorMessage. Our fix suppresses this.
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "cancelled on-chain monitor must not overwrite Lightning tab error state");
            vm.ErrorMessage.Should().NotContain("monitoring was cancelled");
        }

        // ── Step 5: Switch back to on-chain, then back to Lightning ──
        Log("[5] Tab round-trip: OnChain → Lightning...");
        vm.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();
        // The OnChain flow starts immediately and may set its own error (e.g. "No wallet available").
        // The key check is that the previous Lightning error was cleared — any new error is from OnChain.
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("Lightning",
                "previous Lightning error should not persist after switching to OnChain tab");
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "cancelled monitoring error should not bleed through tab switch");
        }
        vm.IsOnChainTab.Should().BeTrue();

        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        // Lightning flow starts async and may fail quickly (e.g. no wallet).
        // The key check is that the previous OnChain error was cleared.
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "OnChain monitoring error should not persist after switching to Lightning tab");
        }
        vm.IsLightningTab.Should().BeTrue();

        // ── Step 6: Stub tabs don't crash ──
        Log("[6] Stub tabs (Liquid, Import) don't crash...");
        vm.SelectNetworkTab(NetworkTab.Liquid);
        Dispatcher.UIThread.RunJobs();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Liquid);
        vm.InvoiceFieldLabel.Should().Be("Liquid Address");
        vm.IsProcessing.Should().BeFalse("stub tabs don't start async work");

        vm.SelectNetworkTab(NetworkTab.Import);
        Dispatcher.UIThread.RunJobs();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Import);
        vm.InvoiceFieldLabel.Should().Be("Imported Invoice");

        // ── Step 7: CloseModal full reset ──
        Log("[7] CloseModal resets all Lightning state...");
        vm.CloseModal();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        vm.IsProcessing.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.OnChainAddress.Should().BeNull();
        vm.LightningInvoice.Should().BeNull();
        vm.LightningSwapId.Should().BeNull();
        vm.IsGeneratingLightningInvoice.Should().BeFalse();
        vm.PaymentReceived.Should().BeFalse();

        window.Close();
        Log("========== 1-click invest LIGHTNING test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task PortfolioDeduplication_AfterInvestment()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestLightningTest) + "_Dedup");
        Log("========== STARTING portfolio deduplication test ==========");

        var window = TestHelpers.CreateShellWindow();
        var services = global::App.App.Services;

        var portfolioVm = services.GetRequiredService<PortfolioViewModel>();
        var currencyService = services.GetRequiredService<ICurrencyService>();

        var project = CreateTestProject("Fund");

        // Simulate adding an investment from the invest flow (optimistic add)
        var initialCount = portfolioVm.Investments.Count;
        portfolioVm.AddInvestmentFromProject(project, "0.00100000");

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "first add should insert a new entry");

        var firstEntry = portfolioVm.Investments[0];
        firstEntry.ProjectIdentifier.Should().Be(project.ProjectId);
        firstEntry.StatusText.Should().Contain("Active",
            "below-threshold Fund investment should be auto-approved as active");

        // Simulate the race: add same project again (as if SDK load returned it)
        portfolioVm.AddInvestmentFromProject(project, "0.00100000");

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "second add of the same project must NOT create a duplicate — " +
            "the deduplication fix should detect the existing entry by ProjectIdentifier");

        window.Close();
        Log("========== Portfolio deduplication test PASSED ==========");
    }

    private static ProjectItemViewModel CreateTestProject(string projectType) => new()
    {
        ProjectId = "headless-lightning-test-project",
        ProjectName = "Headless Lightning Test",
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

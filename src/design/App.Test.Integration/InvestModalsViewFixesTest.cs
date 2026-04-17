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
/// Fast headless smoke test for the recent InvestModalsView fixes.
///
/// Asserts (no testnet / faucet / Boltz needed):
///   1. HasError ↔ ErrorMessage binding is wired (drives the red error banner inside the Invoice modal).
///   2. ShowInvoice() defaults to the On-Chain tab and synchronously flips IsProcessing + PaymentStatusText
///      so the user sees immediate progress instead of a frozen placeholder.
///   3. InvoiceString follows the live PaymentStatusText (not the static "Lightning invoices coming soon"
///      placeholder it used before) so the QR field shows real progress text.
///   4. SelectNetworkTab(Lightning) synchronously sets IsGeneratingLightningInvoice + the Lightning-flavoured
///      status text — this is the fix for "nothing happens when I tap Lightning".
///   5. InvoiceFieldLabel and InvoiceTabIcon follow the active tab.
///   6. With no wallets loaded, errors come back labeled ("No wallet available...") and not as raw
///      ArgumentNullException("Value cannot be null. (Parameter 'key')") strings — i.e. our defensive
///      pre-checks fire before any SDK call that could throw an unwrapped null-key exception.
///
/// The test constructs an InvestPageViewModel directly from DI to keep the run under a couple of seconds —
/// it still boots the full app (so DI / Avalonia / SDK wiring is exercised) but does not navigate the UI.
/// </summary>
public class InvestModalsViewFixesTest
{
    [AvaloniaFact]
    public async Task InvestModals_FixesAreWired()
    {
        using var profileScope = TestProfileScope.For(nameof(InvestModalsViewFixesTest));
        Log("========== STARTING InvestModals fixes smoke test ==========");

        var window = TestHelpers.CreateShellWindow();

        // Resolve services from the live DI container so we exercise real wiring.
        var services = global::App.App.Services;
        var walletAppService = services.GetRequiredService<IWalletAppService>();
        var investmentAppService = services.GetRequiredService<IInvestmentAppService>();
        var portfolioVm = services.GetRequiredService<PortfolioViewModel>();
        var currencyService = services.GetRequiredService<ICurrencyService>();
        var walletContext = services.GetRequiredService<IWalletContext>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<InvestPageViewModel>();

        // Minimal valid project. We don't deploy/lookup — we only drive the VM state machine.
        var project = new ProjectItemViewModel
        {
            ProjectId = "headless-fixes-test-project",
            ProjectName = "Headless Fixes Test",
            ProjectType = "Fund",
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

        var vm = new InvestPageViewModel(
            project,
            walletAppService,
            investmentAppService,
            portfolioVm,
            currencyService,
            walletContext,
            logger);

        // ── Fix 1: HasError binding drives the new Invoice-modal error banner ──
        Log("[1] HasError binding...");
        vm.HasError.Should().BeFalse("no error initially");
        vm.ErrorMessage = "synthetic test error";
        vm.HasError.Should().BeTrue("HasError must flip when ErrorMessage is set so the banner shows");
        vm.ErrorMessage = null;
        vm.HasError.Should().BeFalse("HasError must clear when ErrorMessage is reset");

        // Provide an investment amount so the form is in a valid state for ShowInvoice.
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();

        // ── Fix 2 + 3: ShowInvoice immediate UX ──
        Log("[2] ShowInvoice synchronous state...");
        vm.ShowInvoice();
        vm.CurrentScreen.Should().Be(InvestScreen.Invoice, "ShowInvoice advances to the Invoice screen");
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "On-Chain is the default tab");
        vm.IsProcessing.Should().BeTrue("ShowInvoice flips IsProcessing synchronously so the spinner shows immediately");
        vm.PaymentStatusText.Should().Be("Generating invoice address...",
            "synchronous status text gives the user immediate feedback instead of a frozen placeholder");
        vm.InvoiceString.Should().Be(vm.PaymentStatusText,
            "InvoiceString now follows the live PaymentStatusText — fixes the 'stuck on Generating address' bug");
        vm.InvoiceFieldLabel.Should().Be("On-Chain Address");
        vm.InvoiceTabIcon.Should().Contain("bitcoin", "On-Chain tab uses the bitcoin glyph");

        // Let the async PayViaInvoiceAsync run so the labeled defensive error surfaces.
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.OnChainAddress != null,
            TimeSpan.FromSeconds(5));

        // ── Fix 6: defensive labeled error (not a raw ArgumentNullException) ──
        Log($"[6] On-chain ErrorMessage after pump: '{vm.ErrorMessage}'");
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("Parameter 'key'",
                "the wrapped pre-checks must turn raw ArgumentNullException into a labeled message");
            // With no wallets in the clean profile, we expect the no-wallet labeled error.
            vm.ErrorMessage.Should().ContainAny(new[] { "No wallet available", "Wallet has no ID", "Project has no ID", "Refresh wallet failed", "GetNextReceiveAddress" },
                "errors must be tagged with the step that produced them so we know which call broke");
        }

        // Reset error state before the Lightning leg so we can inspect it independently.
        vm.ErrorMessage = null;

        // ── Fix 4: Lightning tab synchronous state ──
        Log("[4] SelectNetworkTab(Lightning) synchronous state...");
        vm.SelectNetworkTab(NetworkTab.Lightning);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        vm.IsProcessing.Should().BeTrue("Lightning tap must flip IsProcessing synchronously");
        vm.IsGeneratingLightningInvoice.Should().BeTrue(
            "Lightning tap must flip the spinner flag synchronously — fixes 'nothing happens when I tap'");
        vm.PaymentStatusText.Should().Be("Creating Lightning invoice...",
            "synchronous Lightning status text replaces the stale 'Tap Lightning to generate' placeholder");
        vm.InvoiceString.Should().Be("Creating Lightning invoice...",
            "InvoiceString reflects Lightning progress");

        // ── Fix 5: tab-driven label/icon flip ──
        Log("[5] InvoiceFieldLabel + InvoiceTabIcon flip with the active tab...");
        vm.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        vm.InvoiceTabIcon.Should().Contain("bolt", "Lightning tab uses the bolt glyph");

        // Pump again to let PayViaLightningAsync complete its labeled-error path.
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.LightningInvoice != null,
            TimeSpan.FromSeconds(5));

        Log($"[6] Lightning ErrorMessage after pump: '{vm.ErrorMessage}'");
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("Parameter 'key'",
                "Lightning path must also turn raw ArgumentNullException into a labeled message");
            vm.ErrorMessage.Should().ContainAny(new[] { "No wallet available", "Wallet has no ID", "Project has no ID", "Refresh wallet failed", "GetNextReceiveAddress", "CreateLightningSwap" },
                "errors must be tagged with the step that produced them");
        }

        // Liquid + Import tabs are visual stubs — switching to them does not crash and surfaces a known label.
        Log("[7] Stub tabs do not crash...");
        vm.SelectNetworkTab(NetworkTab.Liquid);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Liquid);
        vm.InvoiceFieldLabel.Should().Be("Liquid Address");
        vm.SelectNetworkTab(NetworkTab.Import);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Import);
        vm.InvoiceFieldLabel.Should().Be("Imported Invoice");

        window.Close();
        Log("========== InvestModals fixes smoke test PASSED ==========");
    }

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
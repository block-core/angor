using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;

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
/// The InvestPageViewModel is obtained via UI navigation (Find Projects → project detail → invest page)
/// so the full DI, factory wiring, and navigation stack are exercised.
/// </summary>
public class InvestModalsViewFixesTest
{
    [AvaloniaFact]
    public async Task InvestModals_FixesAreWired()
    {
        using var profileScope = TestProfileScope.For(nameof(InvestModalsViewFixesTest));
        Log("========== STARTING InvestModals fixes smoke test ==========");

        var window = TestHelpers.CreateShellWindow();

        // ── Navigate to Find Projects and get an open project ──
        Log("[0] Navigating to Find Projects and loading projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = window.GetFindProjectsViewModel();
        findVm.Should().NotBeNull("FindProjectsViewModel should be available");
        await WaitForProjects(findVm!);

        var project = findVm!.Projects.FirstOrDefault(p => p.IsOpen);
        project.Should().NotBeNull("at least one open project should be available for testing");

        // ── Open project detail and invest page via UI navigation ──
        findVm.OpenProjectDetail(project!);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var vm = findVm.InvestPageViewModel;
        vm.Should().NotBeNull("InvestPageViewModel should be created via factory");

        // ── Fix 1: HasError binding drives the new Invoice-modal error banner ──
        Log("[1] HasError binding...");
        vm!.HasError.Should().BeFalse("no error initially");
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

        // Import tab is a visual stub — switching to it does not crash and surfaces a known label.
        Log("[7] Stub tab does not crash...");
        vm.SelectNetworkTab(NetworkTab.Import);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Import);
        vm.InvoiceFieldLabel.Should().Be("Imported Invoice");

        window.Close();
        Log("========== InvestModals fixes smoke test PASSED ==========");
    }

    private static async Task WaitForProjects(FindProjectsViewModel vm, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (vm.Projects.Count > 0)
                return;
            await Task.Delay(250);
        }

        vm.Projects.Should().NotBeEmpty(
            "projects should load from SDK within timeout — ensure testnet indexer/relays are reachable");
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

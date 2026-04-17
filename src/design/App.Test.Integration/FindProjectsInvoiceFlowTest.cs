using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shell;
using Angor.Sdk.Wallet.Application;
using App.UI.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// End-to-end headless test for the "1-click" / invoice invest flow.
///
/// Scenario: on the project detail page the user clicks Invest, enters an
/// amount, then instead of paying from an existing wallet selects
/// "Pay an invoice instead" — the UI generates an on-chain receive address
/// and monitors it for incoming funds. In this test the faucet pays that
/// address directly (mimicking an external wallet / QR-scanned payment).
///
/// Asserts:
///   - Flow transitions: InvestForm → WalletSelector → Invoice
///   - PayViaInvoice reaches "Waiting for payment..." state
///   - External faucet payment is detected (PaymentReceived flips true)
///   - Investment is published and the Success screen is reached
///
/// Requires real testnet infrastructure (indexer + faucet + Nostr relays).
/// </summary>
public class FindProjectsInvoiceFlowTest
{
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InvoicePaymentTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task PayViaInvoice_OnChain_FaucetPaysAddress_ReachesSuccess()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsInvoiceFlowTest));
        Log("========== STARTING PayViaInvoice on-chain flow test ==========");

        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Invoice Flow {runId}";
        var projectAbout = $"Invoice/QR flow test {runId}. External faucet pays the invoice address.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        // ── Step 1: Boot app, wipe, create wallet, fund ──
        var window = TestHelpers.CreateShellWindow();

        Log("[STEP 1] Wiping data and creating wallet...");
        await WipeExistingData(window);
        await CreateWalletViaGenerate(window);
        await FundWalletViaFaucet(window);

        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");

        // ── Step 2: Create + deploy a fund project so we have something to invest in ──
        Log("[STEP 2] Creating and deploying fund project...");
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!);
        var wizardVm = myProjectsVm!.CreateProjectVm!;

        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        wizardVm.SelectProjectType("fund");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        wizardVm.TargetAmount = "1.0";
        wizardVm.ApprovalThreshold = "0.5";
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.PayoutFrequency = "Weekly";
        wizardVm.ToggleInstallmentCount(3);
        wizardVm.WeeklyPayoutDay = DateTime.UtcNow.DayOfWeek.ToString();
        wizardVm.GeneratePayoutSchedule();
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        await Task.Delay(500);
        wizardVm.Deploy();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();

        var deployVm = wizardVm.DeployFlow;
        deployVm.IsVisible.Should().BeTrue("Deploy overlay should be visible");

        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline && deployVm.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
        deployVm.Wallets.Count.Should().BeGreaterThan(0, "should have at least one wallet");

        deployVm.SelectWallet(deployVm.Wallets[0]);
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 2] Deploy payment initiated...");
        deployVm.PayWithWallet();

        var deploySuccessDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < deploySuccessDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (deployVm.CurrentScreen == DeployScreen.Success) break;
            if (!deployVm.IsDeploying && deployVm.DeployStatusText.Contains("Failed")) break;
            await Task.Delay(PollInterval);
        }
        deployVm.CurrentScreen.Should().Be(DeployScreen.Success,
            $"deploy should succeed. Status: {deployVm.DeployStatusText}");
        Log("[STEP 2] Project deployed successfully.");

        deployVm.GoToMyProjects();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();

        var shellVm = window.GetShellViewModel();
        if (shellVm.IsModalOpen)
        {
            shellVm.HideModal();
            Dispatcher.UIThread.RunJobs();
        }

        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        // ── Step 3: Find the project on the indexer ──
        Log("[STEP 3] Finding project in Find Projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = GetFindProjectsViewModel(window);
        findVm.Should().NotBeNull();

        ProjectItemViewModel? foundProject = null;
        var findDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < findDeadline)
        {
            await findVm!.LoadProjectsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();
            foundProject = findVm.Projects.FirstOrDefault(p =>
                p.ShortDescription.Contains(runId) || p.ProjectName.Contains(runId));
            if (foundProject != null) break;
            Log($"[STEP 3] Project not found yet ({findVm.Projects.Count} loaded). Retrying...");
            await Task.Delay(PollInterval);
        }
        foundProject.Should().NotBeNull($"should find project with runId '{runId}'");
        Log($"[STEP 3] Found project: '{foundProject!.ProjectName}'");

        // ── Step 4: Open invest page, enter amount, submit to wallet selector ──
        Log("[STEP 4] Opening invest page & submitting amount...");
        findVm!.OpenProjectDetail(foundProject);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = findVm.InvestPageViewModel!;
        investVm.CurrentScreen.Should().Be(InvestScreen.InvestForm, "flow should start on the invest form");

        // Small amount below the 0.5 threshold → will auto-approve (publish directly)
        investVm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();

        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector,
            "submitting the amount should advance to the wallet selector");

        // ── Step 5: Choose the invoice/QR path instead of a wallet ──
        Log("[STEP 5] Choosing 'Pay an invoice instead' (QR/on-chain) path...");
        investVm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.Invoice,
            "ShowInvoice should advance to the invoice/QR screen");
        investVm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain,
            "On-Chain is the default tab when the Invoice screen opens");

        // ShowInvoice auto-starts the on-chain monitor (PayViaInvoice) — no explicit kick needed.

        // Wait for the VM to advance to "Waiting for payment..." so we know
        // the invoice address has been generated and monitoring has started.
        var waitForInvoiceDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < waitForInvoiceDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.IsProcessing && investVm.PaymentStatusText.Contains("Waiting for payment"))
                break;
            if (investVm.ErrorMessage != null)
                throw new Exception($"PayViaInvoice errored before monitoring started: {investVm.ErrorMessage}");
            await Task.Delay(500);
        }
        investVm.PaymentStatusText.Should().Contain("Waiting for payment",
            "PayViaInvoice should have generated the address and be monitoring for funds");
        Log("[STEP 5] Invoice monitoring active. Status: " + investVm.PaymentStatusText);

        // ── Step 6: Pay the invoice address via the faucet (external payer) ──
        // The VM monitors the wallet's next unused receive address. Reading it
        // here returns the same address (pure read — no pointer mutation) so we
        // can hand it to the faucet exactly as a QR-scan payer would.
        Log("[STEP 6] Fetching invoice address and paying it via faucet...");
        var walletContext = global::App.App.Services.GetRequiredService<IWalletContext>();
        var walletAppService = global::App.App.Services.GetRequiredService<IWalletAppService>();
        var walletId = walletContext.Wallets.First().Id;

        var addressResult = await walletAppService.GetNextReceiveAddress(walletId);
        addressResult.IsSuccess.Should().BeTrue($"should read invoice address. Error: {(addressResult.IsFailure ? addressResult.Error : "")}");
        var invoiceAddress = addressResult.Value.Value;
        Log($"[STEP 6] Invoice address: {invoiceAddress}");

        using var http = new HttpClient();
        var faucetResponse = await http.GetAsync($"https://faucettmp.angor.io/api/faucet/send/{invoiceAddress}/2");
        faucetResponse.IsSuccessStatusCode.Should().BeTrue(
            $"faucet should accept the payment request. Status: {faucetResponse.StatusCode}, Body: {await faucetResponse.Content.ReadAsStringAsync()}");
        Log("[STEP 6] Faucet accepted payment to invoice address.");

        // ── Step 7: Wait for the VM to detect the payment and finish publishing ──
        Log("[STEP 7] Waiting for PaymentReceived + Success screen...");
        var invoiceDeadline = DateTime.UtcNow + InvoicePaymentTimeout;
        var observedStatusTexts = new List<string>();
        while (DateTime.UtcNow < invoiceDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (!observedStatusTexts.Contains(investVm.PaymentStatusText))
            {
                observedStatusTexts.Add(investVm.PaymentStatusText);
                Log($"[STEP 7] Status: '{investVm.PaymentStatusText}' (Received={investVm.PaymentReceived})");
            }

            if (investVm.CurrentScreen == InvestScreen.Success) break;
            if (!investVm.IsProcessing && investVm.ErrorMessage != null)
            {
                Log($"[STEP 7] Error: {investVm.ErrorMessage}");
                break;
            }
            await Task.Delay(2000);
        }

        // ── Assertions ──
        investVm.PaymentReceived.Should().BeTrue(
            "the faucet payment to the invoice address should have been detected");
        observedStatusTexts.Should().Contain(s => s.Contains("Payment received"),
            "status should have flipped through 'Payment received!' on the way to publishing");

        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"invoice flow should reach Success. Error: {investVm.ErrorMessage ?? "none"}. Last status: {investVm.PaymentStatusText}");
        investVm.IsSuccess.Should().BeTrue();
        investVm.SuccessTitle.Should().NotBeNullOrWhiteSpace();
        investVm.SuccessDescription.Should().NotBeNullOrWhiteSpace();

        // 0.001 < 0.5 threshold → auto-approved (published directly)
        investVm.IsAutoApproved.Should().BeTrue(
            "0.001 BTC is below the 0.5 BTC penalty threshold — should publish directly");

        window.Close();
        Log("========== PayViaInvoice on-chain flow test PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers (mirrored from FindProjectsPaymentFlowTest)
    // ═══════════════════════════════════════════════════════════════════

    private static FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        var view = window.GetVisualDescendants().OfType<FindProjectsView>().FirstOrDefault();
        return view?.DataContext as FindProjectsViewModel;
    }

    private static MyProjectsViewModel? GetMyProjectsViewModel(Window window)
    {
        var view = window.GetVisualDescendants().OfType<MyProjectsView>().FirstOrDefault();
        return view?.DataContext as MyProjectsViewModel;
    }

    private static async Task OpenCreateWizard(Window window, MyProjectsViewModel vm)
    {
        vm.CreateProjectVm.ResetWizard();
        vm.LaunchCreateWizard();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        vm.CreateProjectVm.OnProjectDeployed = () =>
        {
            vm.OnProjectDeployed(vm.CreateProjectVm);
            vm.CloseCreateWizard();
        };
    }

    private static async Task WipeExistingData(Window window)
    {
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants().OfType<SettingsView>().FirstOrDefault();
        if (settingsView?.DataContext is SettingsViewModel settingsVm)
        {
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task CreateWalletViaGenerate(Window window)
    {
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var addWalletBtn = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.IsVisible)
            .FirstOrDefault(b =>
                (b.Content is string text && text.Contains("Add Wallet")) ||
                (b.Content is StackPanel sp && sp.Children.OfType<TextBlock>().Any(tb => tb.Text == "Add Wallet")));
        addWalletBtn.Should().NotBeNull("should find Add Wallet button");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        await window.ClickButton("BtnGenerate", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        await window.ClickButton("BtnDownloadSeed", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        await window.ClickButton("BtnContinueBackup", UiTimeout);
        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("wallet creation should succeed");

        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task FundWalletViaFaucet(Window window)
    {
        var walletCard = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCard.Should().NotBeNull("WalletCard should appear after wallet creation");

        var faucetBtn = await window.WaitForControl<Button>("WalletCardBtnFaucet", UiTimeout);
        faucetBtn.Should().NotBeNull();
        faucetBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, faucetBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(5000);

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var refreshBtn = await window.WaitForControl<Button>("WalletCardBtnRefresh", UiTimeout);
            if (refreshBtn != null)
            {
                refreshBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, refreshBtn));
                Dispatcher.UIThread.RunJobs();
            }

            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();

            var fundsView = window.GetVisualDescendants().OfType<FundsView>().FirstOrDefault();
            var fundsVm = fundsView?.DataContext as FundsViewModel;
            if (fundsVm != null && fundsVm.TotalBalance != "0.0000")
            {
                Log($"  Balance: {fundsVm.TotalBalance}");
                return;
            }
        }

        throw new TimeoutException("Faucet balance did not appear within timeout");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

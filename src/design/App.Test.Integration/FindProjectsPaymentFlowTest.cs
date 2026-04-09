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
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// End-to-end headless test for the Find Projects payment flow.
/// Creates a wallet, funds it, deploys a fund-type project, then invests
/// in it — asserting the specific UI state transitions during payment:
///
///   - PayWithWallet shows processing states (IsProcessing, PaymentStatusText)
///   - Success screen renders with correct title/description
///   - AddToPortfolio updates PortfolioViewModel
///
/// This test requires real testnet infrastructure (indexer + faucet + Nostr relays)
/// and takes 2-5 minutes.
/// </summary>
public class FindProjectsPaymentFlowTest
{
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task PayWithWallet_ShowsProcessingStates_ThenSuccess_ThenAddsToPortfolio()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPaymentFlowTest));
        Log("========== STARTING PayWithWallet flow test ==========");

        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Pay Flow {runId}";
        var projectAbout = $"Payment flow test {runId}. Tests processing states, success screen, and add-to-portfolio.";
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

        // ── Step 2: Create + deploy fund project ──
        Log("[STEP 2] Creating and deploying fund project...");
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!);
        var wizardVm = myProjectsVm!.CreateProjectVm!;

        // Wizard: type → profile → images → funding → stages → review → deploy
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

        // Review → deploy
        await Task.Delay(500);
        wizardVm.Deploy();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();

        var deployVm = wizardVm.DeployFlow;
        deployVm.IsVisible.Should().BeTrue("Deploy overlay should be visible");

        // Wait for wallets to load
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
            if (!deployVm.IsDeploying && deployVm.DeployStatusText.Contains("Failed"))
                break;
            await Task.Delay(PollInterval);
        }
        deployVm.CurrentScreen.Should().Be(DeployScreen.Success,
            $"deploy should succeed. Status: {deployVm.DeployStatusText}");
        Log("[STEP 2] Project deployed successfully.");

        deployVm.GoToMyProjects();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();

        // Close shell modal if still open
        var shellVm = window.GetShellViewModel();
        if (shellVm.IsModalOpen)
        {
            shellVm.HideModal();
            Dispatcher.UIThread.RunJobs();
        }

        // Reload founder projects to populate ProjectIdentifier
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        // ── Step 3: Find our project in Find Projects ──
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

        // ── Step 4: Invest — assert processing states ──
        Log("[STEP 4] Opening invest page...");
        findVm!.OpenProjectDetail(foundProject);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = findVm.InvestPageViewModel!;

        // Use a small amount below the high threshold so it auto-approves
        investVm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();

        investVm.SelectWallet(investVm.Wallets[0]);
        Dispatcher.UIThread.RunJobs();

        // Fire PayWithWallet — track processing states
        Log("[STEP 4] Firing PayWithWallet...");
        var observedStatusTexts = new List<string>();
        var wasProcessing = false;

        investVm.PayWithWallet();

        var payDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < payDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (investVm.IsProcessing && !wasProcessing)
            {
                wasProcessing = true;
                Log($"[STEP 4] IsProcessing became true. Status: {investVm.PaymentStatusText}");
            }

            if (investVm.IsProcessing && !observedStatusTexts.Contains(investVm.PaymentStatusText))
            {
                observedStatusTexts.Add(investVm.PaymentStatusText);
                Log($"[STEP 4] Status update: '{investVm.PaymentStatusText}'");
            }

            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                Log("[STEP 4] Success screen reached.");
                break;
            }

            if (!investVm.IsProcessing && investVm.ErrorMessage != null)
            {
                Log($"[STEP 4] Error: {investVm.ErrorMessage}");
                break;
            }

            await Task.Delay(500);
        }

        // ── ASSERT: Processing states were observed ──
        wasProcessing.Should().BeTrue("IsProcessing should have been true during payment");
        observedStatusTexts.Should().NotBeEmpty("should have observed status text updates during processing");
        observedStatusTexts.Should().Contain(s => s.Contains("Refreshing") || s.Contains("Building") || s.Contains("Publishing"),
            "should see at least one meaningful processing status");

        // ── ASSERT: Success screen ──
        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"should reach success. Error: {investVm.ErrorMessage ?? "none"}. Last status: {investVm.PaymentStatusText}");
        investVm.IsSuccess.Should().BeTrue();
        investVm.SuccessTitle.Should().NotBeNullOrWhiteSpace("success title should be rendered");
        investVm.SuccessDescription.Should().NotBeNullOrWhiteSpace("success description should be rendered");

        // ── ASSERT: Auto-approval detection ──
        // We used 0.001 with threshold 0.5 → should be auto-approved
        investVm.IsAutoApproved.Should().BeTrue(
            "0.001 BTC is below 0.5 BTC threshold → should be auto-approved for fund-type project");

        // ── Step 5: Add to portfolio ──
        Log("[STEP 5] Adding to portfolio...");
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        var countBefore = portfolioVm.Investments.Count;

        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        portfolioVm.Investments.Count.Should().Be(countBefore + 1,
            "portfolio should have one more investment after AddToPortfolio");
        portfolioVm.HasInvestments.Should().BeTrue();
        Log($"[STEP 5] Portfolio now has {portfolioVm.Investments.Count} investment(s).");

        window.Close();
        Log("========== PayWithWallet flow test PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers (same patterns as FundAndRecoverTest / SendToSelfTest)
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

        // Wire deploy completed callback (normally done by code-behind)
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

        // Request faucet
        var faucetBtn = await window.WaitForControl<Button>("WalletCardBtnFaucet", UiTimeout);
        faucetBtn.Should().NotBeNull();
        faucetBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, faucetBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(5000);

        // Poll refresh until balance > 0
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

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// Full end-to-end integration test that boots the real app in headless mode,
/// creates a wallet, funds it via testnet faucet, creates a fund-type
/// project through the 6-step wizard, invests in that project above the penalty
/// threshold so founder approval is required, confirms the investment after the
/// founder approves it, has the founder spend stage 1, and then recovers the
/// remaining invested funds.
///
/// Steps:
///   1. Wipe any existing data (via Settings → Danger Zone → Wipe Data)
///   2. Navigate to Funds → create wallet (Generate path)
///   3. Fund wallet via faucet, wait for non-zero balance
///   4. Navigate to My Projects → create + deploy fund project (6-step wizard)
///   5. Navigate to Find Projects → reload → find our project by unique GUID
///   6. Open invest page → set above-threshold amount → submit → select wallet → pay → success
///   7. Add investment to portfolio
///   8. Navigate to Funders → founder approves the pending investment request
///   9. Navigate to Funded → reload investments → confirm the signed investment
///  10. Navigate to My Projects → founder spends stage 1
///  11. Navigate to Funded → reload investments → find our investment
///  12. Load recovery status → execute appropriate recovery action
///  13. Verify recovery succeeded
///
/// A unique GUID is embedded in the project description so we can precisely identify
/// the project we created, even if other projects exist in the list.
///
/// This test uses real testnet infrastructure (indexer + faucet API + Nostr relays)
/// and requires internet connectivity. It may take 120-300 seconds depending on
/// network conditions.
///
/// All UI interactions use AutomationProperties.AutomationId where available,
/// with ViewModel-direct calls for controls that use PointerPressed on Border
/// elements (type cards, wallet selector) or ListBox selections.
/// </summary>
public class FundAndRecoverTest
{
    /// <summary>
    /// Maximum time to wait for the faucet coins to appear in the wallet balance.
    /// </summary>
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum time to wait for deploy/invest transactions to complete.
    /// </summary>
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Standard timeout for UI controls to appear after navigation or modal actions.
    /// </summary>
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Interval between balance refresh polls.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum time to wait for the investment to appear in the SDK after investing.
    /// The indexer may lag behind the blockchain significantly on signet — unconfirmed
    /// transactions can take 30-180+ seconds to propagate to the mempool.space indexer.
    /// </summary>
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task FullFundAndRecoverFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(FundAndRecoverTest));
        Log("========== STARTING FullFundAndRecoverFlow ==========");

        // Generate a unique run ID so we can precisely identify *our* project
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Test Fund {runId}";
        var projectAbout = $"Automated fund-and-recover test run {runId}. Verifies fund creation, founder spend, and investor recovery.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        // Wizard input parameters
        var targetAmountBtc = "1.0";
        var approvalThresholdBtc = "0.01";
        var payoutFrequency = "Weekly";
        var installmentCount = 3;
        var weeklyPayoutDay = DateTime.UtcNow.DayOfWeek.ToString();

        // Investment amount — above approval threshold (0.01 BTC) so founder approval is required
        var investmentAmountBtc = "0.02";

        Log($"[STEP 0] Run ID: {runId}");
        Log($"[STEP 0] Project name: {projectName}");
        Log($"[STEP 0] Investment amount: {investmentAmountBtc} BTC (above approval threshold)");

        // ──────────────────────────────────────────────────────────────
        // ARRANGE: Boot the full app with ShellView
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 0] Booting app with ShellView...");
        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();
        Log("[STEP 0] App booted. ShellView created, ShellViewModel ready.");

        // ──────────────────────────────────────────────────────────────
        // STEP 1: Wipe any existing data to start clean
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 1] Wiping existing data...");
        await WipeExistingData(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 2: Navigate to Funds → create wallet via Generate path
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 2] Navigating to Funds section...");
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", UiTimeout);
        Log($"[STEP 2] EmptyStatePanel found: {emptyState != null}");
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        Log("[STEP 2] Creating wallet via Generate path...");
        await CreateWalletViaGenerate(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 3: Wait for WalletCard, fund via faucet, wait for balance
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 3] Waiting for WalletCard to appear...");
        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        Log("[STEP 3] Requesting testnet coins and waiting for balance...");
        await FundWalletViaFaucet(window);

        // Set the password provider key so the SDK can decrypt the wallet for all subsequent operations
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log("[STEP 3] Set SimplePasswordProvider key to 'default-key'.");

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Create + deploy fund project
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 4] Navigating to My Projects section...");
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull("MyProjectsViewModel should be available");

        Log("[STEP 4] Opening create wizard...");
        await OpenCreateWizard(window, myProjectsVm!);

        var wizardVm = myProjectsVm!.CreateProjectVm;
        wizardVm.Should().NotBeNull("CreateProjectViewModel should exist");

        // Step 1: Dismiss welcome, select "fund" type
        Log("[STEP 4.1] Selecting 'fund' project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("fund");
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 2: Project profile
        Log("[STEP 4.2] Setting project name and about...");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 3: Images
        Log("[STEP 4.3] Setting banner and profile images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4: Funding config
        Log("[STEP 4.4] Setting target amount and approval threshold...");
        wizardVm.TargetAmount = targetAmountBtc;
        wizardVm.ApprovalThreshold = approvalThresholdBtc;
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 5: Payout schedule
        Log("[STEP 4.5] Setting payout schedule (3 weekly payouts)...");
        wizardVm.ShowStep5Welcome.Should().BeTrue("Step 5 should start with welcome screen");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.PayoutFrequency = payoutFrequency;
        wizardVm.ToggleInstallmentCount(installmentCount);
        wizardVm.WeeklyPayoutDay = weeklyPayoutDay;
        Dispatcher.UIThread.RunJobs();
        wizardVm.GeneratePayoutSchedule();
        Dispatcher.UIThread.RunJobs();
        wizardVm.Stages.Count.Should().Be(installmentCount);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 6: Deploy
        Log("[STEP 4.6] Deploying project...");
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
        deployVm.Wallets.Count.Should().BeGreaterThan(0, "At least one wallet should be loaded");

        var deployWallet = deployVm.Wallets[0];
        Log($"[STEP 4.6] Selecting wallet: {deployWallet.Name} (balance: {deployWallet.Balance})...");
        deployVm.SelectWallet(deployWallet);
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.6] Paying with wallet (SDK deploy pipeline)...");
        deployVm.PayWithWallet();

        var deployDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (deployVm.CurrentScreen == DeployScreen.Success)
            {
                Log("[STEP 4.6] Deploy succeeded!");
                break;
            }
            if (!deployVm.IsDeploying && deployVm.CurrentScreen != DeployScreen.Success)
            {
                Log($"[STEP 4.6] Deploy status: {deployVm.DeployStatusText}");
                if (deployVm.DeployStatusText.Contains("Failed") || deployVm.DeployStatusText.Contains("error"))
                    break;
            }
            await Task.Delay(PollInterval);
        }
        deployVm.CurrentScreen.Should().Be(DeployScreen.Success,
            $"Deploy should reach success. Last status: {deployVm.DeployStatusText}");

        deployVm.GoToMyProjects();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();
        if (shellVm.IsModalOpen) { shellVm.HideModal(); Dispatcher.UIThread.RunJobs(); }

        // Verify project deployed
        var project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull($"Project with run ID '{runId}' should appear in My Projects");
        Log($"[STEP 4.6] Project deployed: '{project!.Name}' (ID: {project.ProjectIdentifier})");

        Log("[STEP 4.7] Reloading founder projects from SDK to populate identifiers...");
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull("Project should still exist after founder reload");
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty("Founder reload should populate ProjectIdentifier");
        project.OwnerWalletId.Should().NotBeNullOrEmpty("Founder reload should populate OwnerWalletId");
        Log($"[STEP 4.7] Reloaded founder project: projectId='{project.ProjectIdentifier}', ownerWalletId='{project.OwnerWalletId}'");

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Navigate to Find Projects → find our project
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 5] Navigating to Find Projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findProjectsVm = GetFindProjectsViewModel(window);
        findProjectsVm.Should().NotBeNull("FindProjectsViewModel should be available");

        // Reload projects from SDK (indexer may need a moment to pick up the new project)
        Log("[STEP 5] Loading projects from SDK...");
        ProjectItemViewModel? foundProject = null;
        var findDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < findDeadline)
        {
            await findProjectsVm!.LoadProjectsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            foundProject = findProjectsVm.Projects.FirstOrDefault(p =>
                p.Description.Contains(runId) || p.ShortDescription.Contains(runId));
            if (foundProject != null) break;

            Log($"[STEP 5] Project not found yet in SDK ({findProjectsVm.Projects.Count} project(s) loaded). Retrying...");
            await Task.Delay(PollInterval);
        }

        foundProject.Should().NotBeNull($"Should find our project (run ID '{runId}') in Find Projects from SDK");
        Log($"[STEP 5] Found project: '{foundProject!.ProjectName}' (ID: {foundProject.ProjectId})");

        // Additional VM assertions on the found project
        foundProject.ProjectName.Should().Be(projectName, "ProjectName should match what we set in the wizard");
        foundProject.ProjectType.Should().Be("Fund", "Fund-type project should have ProjectType 'Fund'");
        foundProject.ProjectId.Should().NotBeNullOrEmpty("ProjectId should be populated from SDK");

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Open invest page → invest above threshold
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 6] Opening project detail...");
        findProjectsVm!.OpenProjectDetail(foundProject);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);

        Log("[STEP 6] Opening invest page...");
        findProjectsVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var investVm = findProjectsVm.InvestPageViewModel;
        investVm.Should().NotBeNull("InvestPageViewModel should be created");
        Log($"[STEP 6] InvestPageViewModel created for project '{investVm!.Project.ProjectName}'");

        // Wait for wallets to load in the invest page
        Log("[STEP 6] Waiting for wallets to load in invest page...");
        var investWalletDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < investWalletDeadline && investVm.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
        investVm.Wallets.Count.Should().BeGreaterThan(0, "At least one wallet should be available for investing");
        Log($"[STEP 6] {investVm.Wallets.Count} wallet(s) loaded");

        // Set investment amount
        Log($"[STEP 6] Setting investment amount to {investmentAmountBtc} BTC...");
        investVm.InvestmentAmount = investmentAmountBtc;
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue($"Should be able to submit with amount {investmentAmountBtc}");

        // Submit → wallet selector
        Log("[STEP 6] Submitting invest form...");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector, "Should advance to wallet selector after submit");

        // Select wallet
        var investWallet = investVm.Wallets[0];
        Log($"[STEP 6] Selecting wallet: '{investWallet.Name}' (balance: {investWallet.Balance})...");
        investVm.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();
        investVm.SelectedWallet.Should().NotBeNull("Should have a selected wallet");

        // Pay with wallet → SDK pipeline
        Log("[STEP 6] Paying with wallet (SDK invest pipeline)...");
        investVm.PayWithWallet();

        var investDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < investDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                Log("[STEP 6] Investment succeeded! Success screen visible.");
                break;
            }
            if (!investVm.IsProcessing && investVm.CurrentScreen != InvestScreen.Success)
            {
                Log($"[STEP 6] Invest status: {investVm.PaymentStatusText}");
                if (investVm.PaymentStatusText.Contains("Failed") || investVm.PaymentStatusText.Contains("Error"))
                    break;
            }
            await Task.Delay(PollInterval);
        }
        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"Invest should reach success. Last status: {investVm.PaymentStatusText}");

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Add investment to portfolio + verify no duplicates
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 7] Adding investment to portfolio...");
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        portfolioVm.HasInvestments.Should().BeTrue("Portfolio should have at least one investment after AddToPortfolio");
        Log($"[STEP 7] Portfolio now has {portfolioVm.Investments.Count} investment(s)");

        // ── Enhancement 1: Portfolio duplicate check ──
        // After AddToPortfolio the local collection has 1 optimistic entry.
        // Reload from SDK and verify only ONE entry for our project (no duplicates).
        Log("[STEP 7.1] Verifying no duplicate investments after SDK reload...");
        await portfolioVm.LoadInvestmentsFromSdkAsync();
        Dispatcher.UIThread.RunJobs();

        var matchingInvestments = portfolioVm.Investments
            .Where(i => i.ProjectIdentifier == foundProject.ProjectId || i.ProjectName == foundProject.ProjectName)
            .ToList();
        matchingInvestments.Count.Should().Be(1,
            "There should be exactly one investment entry for our project after AddToPortfolio + SDK reload (no duplicates)");

        var localInvestment = matchingInvestments[0];
        Log($"[STEP 7.1] Verified single investment entry: name='{localInvestment.ProjectName}', step={localInvestment.Step}, status='{localInvestment.StatusText}'");

        // Verify investment VM properties after local add
        localInvestment.ProjectName.Should().Be(foundProject.ProjectName, "Investment project name should match");
        localInvestment.ProjectType.Should().Be("fund", "Fund-type project should have ProjectType 'fund'");
        localInvestment.TypeLabel.Should().Be("Funding", "Fund-type investments should show 'Funding' type label");
        localInvestment.Step.Should().Be(1, "Above-threshold fund investment should start at step 1 (awaiting approval)");
        localInvestment.StatusText.Should().Be("Awaiting Approval", "Above-threshold investment should show 'Awaiting Approval'");
        localInvestment.ApprovalStatus.Should().Be("Pending", "Investment should be Pending before founder approval");
        localInvestment.ProjectIdentifier.Should().Be(foundProject.ProjectId, "Investment should reference our project ID");
        localInvestment.CurrencySymbol.Should().NotBeNullOrEmpty("CurrencySymbol should be set (e.g. 'BTC' or 'TBTC')");
        localInvestment.FundingAmount.Should().EndWith(localInvestment.CurrencySymbol,
            "FundingAmount should include the active currency symbol");
        double.TryParse(localInvestment.TotalInvested, NumberStyles.Float, CultureInfo.InvariantCulture, out var totalInvested)
            .Should().BeTrue("TotalInvested should parse as a numeric BTC amount");
        totalInvested.Should().BeGreaterThan(0, "TotalInvested should be positive after investing");

        // ──────────────────────────────────────────────────────────────
        // STEP 8: Founder approves pending investment request
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 8] Founder approving pending investment request...");
        findProjectsVm.CloseInvestPage();
        findProjectsVm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        window.NavigateToSection("Funders");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var fundersVm = GetFundersViewModel(window);
        fundersVm.Should().NotBeNull("FundersViewModel should be available for founder approval flow");

        fundersVm!.SetFilter("waiting");

        SignatureRequestViewModel? pendingRequest = null;
        var approvalRequestDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < approvalRequestDeadline)
        {
            await fundersVm.LoadInvestmentRequestsAsync();
            fundersVm.SetFilter("waiting");
            Dispatcher.UIThread.RunJobs();

            pendingRequest = fundersVm.FilteredSignatures.FirstOrDefault(s =>
                s.ProjectIdentifier == foundProject.ProjectId || s.ProjectTitle == foundProject.ProjectName);
            if (pendingRequest != null)
            {
                break;
            }

            Log("[STEP 8] Pending founder request not visible yet. Retrying...");
            await Task.Delay(PollInterval);
        }

        pendingRequest.Should().NotBeNull("Funders should show the pending request for the invested project");
        Log($"[STEP 8] Approving request {pendingRequest!.EventId} for project '{pendingRequest.ProjectTitle}'");
        fundersVm.ApproveSignature(pendingRequest.Id);
        Dispatcher.UIThread.RunJobs();

        var approvalCompleteDeadline = DateTime.UtcNow + IndexerLagTimeout;
        var founderApproved = false;
        while (DateTime.UtcNow < approvalCompleteDeadline)
        {
            await fundersVm.LoadInvestmentRequestsAsync();
            fundersVm.SetFilter("approved");
            Dispatcher.UIThread.RunJobs();

            founderApproved = fundersVm.FilteredSignatures.Any(s =>
                s.ProjectIdentifier == foundProject.ProjectId || s.ProjectTitle == foundProject.ProjectName);
            if (founderApproved)
            {
                break;
            }

            Log("[STEP 8] Waiting for approved founder signature to appear...");
            await Task.Delay(PollInterval);
        }

        founderApproved.Should().BeTrue("Investment request should move to approved after founder approval");
        Log("[STEP 8] Founder approval completed");

        // ──────────────────────────────────────────────────────────────
        // STEP 9: Investor reloads signed investment and confirms it
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 9] Reloading funded investments and confirming signed investment...");
        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        InvestmentViewModel? signedInvestment = null;
        var signedDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < signedDeadline)
        {
            await portfolioVm.LoadInvestmentsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            signedInvestment = portfolioVm.Investments.FirstOrDefault(i =>
                i.ProjectIdentifier == foundProject.ProjectId || i.ProjectName == foundProject.ProjectName);

            if (signedInvestment != null)
            {
                Log($"[STEP 9] Investment status from SDK: status='{signedInvestment.StatusText}', step={signedInvestment.Step}, approval='{signedInvestment.ApprovalStatus}'");
            }

            if (signedInvestment is { Step: 2 } || signedInvestment?.ApprovalStatus == "Approved")
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        signedInvestment.Should().NotBeNull("Signed investment should appear in the portfolio after founder approval");
        signedInvestment!.Step.Should().Be(2, "Founder approval should move the investment to the signed state before confirmation");

        var confirmResult = await portfolioVm.ConfirmInvestmentAsync(signedInvestment);
        Dispatcher.UIThread.RunJobs();
        confirmResult.Should().BeTrue("Investor confirmation should succeed after founder signatures are available and valid");
        signedInvestment.Step.Should().Be(3, "Confirmed investment should advance to the active state");
        signedInvestment.StatusText.Should().Be("Investment Active");
        signedInvestment.StatusClass.Should().Be("active", "Active investment should have 'active' status class");
        signedInvestment.IsStatusActive.Should().BeTrue("IsStatusActive should be true for confirmed investment");
        signedInvestment.IsStep3.Should().BeTrue("IsStep3 should be true after confirmation");
        signedInvestment.IsStepAtLeast3.Should().BeTrue("IsStepAtLeast3 should be true after confirmation");
        signedInvestment.ProjectType.Should().Be("fund", "Fund-type project type should persist through confirmation");
        Log("[STEP 9] Investor confirmed signed investment successfully");

        // ──────────────────────────────────────────────────────────────
        // STEP 10: Founder spends stage 1
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 10] Founder spending stage 1...");
        findProjectsVm.CloseInvestPage();
        findProjectsVm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var founderProjectsVm = GetMyProjectsViewModel(window);
        founderProjectsVm.Should().NotBeNull("MyProjectsViewModel should be available for founder manage flow");

        founderProjectsVm!.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var manageVm = founderProjectsVm.SelectedManageProject;
        manageVm.Should().NotBeNull("ManageProjectViewModel should be created");

        // ── Enhancement 2: ManageProject stage/UTXO assertions before spending ──
        // Wait for claimable transactions to load, then verify stages look correct BEFORE the founder spends.
        Log("[STEP 10.0] Verifying ManageProject stages before founder spend...");
        var preSpendDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < preSpendDeadline)
        {
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            if (manageVm.Stages.Count >= installmentCount)
                break;

            Log($"[STEP 10.0] Waiting for stages to populate... ({manageVm.Stages.Count}/{installmentCount})");
            await Task.Delay(PollInterval);
        }

        manageVm!.Stages.Count.Should().Be(installmentCount,
            $"ManageProject should show {installmentCount} stages matching the payout schedule");
        Log($"[STEP 10.0] Stage count verified: {manageVm.Stages.Count}");

        // Verify stage numbers are sequential
        var stageNumbers = manageVm.Stages.Select(s => s.Number).OrderBy(n => n).ToList();
        stageNumbers.Should().ContainInOrder(Enumerable.Range(1, installmentCount),
            "Stage numbers should be sequential 1..N");

        // Verify stage 1 is available / first spendable
        var stage1Pre = manageVm.Stages.First(s => s.Number == 1);
        stage1Pre.Available.Should().BeTrue("Stage 1 should be available (has UTXOs)");
        stage1Pre.UnspentTransactionCount.Should().BeGreaterThan(0,
            "Stage 1 should have at least one unspent transaction");
        stage1Pre.SpentTransactionCount.Should().Be(0,
            "Stage 1 should have zero spent transactions before founder claims");
        stage1Pre.AmountLeft.Should().NotBe("0.00000000",
            "Stage 1 AmountLeft should be non-zero before spending");
        stage1Pre.CompletionDate.Should().NotBeNullOrEmpty(
            "Stage 1 should have a completion date");

        // Verify all stages have non-zero amounts
        foreach (var stage in manageVm.Stages)
        {
            stage.AmountLeft.Should().NotBe("0.00000000",
                $"Stage {stage.Number} should have a non-zero amount before any spending");
            stage.UnspentTransactionCount.Should().BeGreaterThan(0,
                $"Stage {stage.Number} should have unspent UTXOs");
            stage.SpentTransactionCount.Should().Be(0,
                $"Stage {stage.Number} should have no spent transactions yet");
            Log($"[STEP 10.0] Stage #{stage.Number}: amount={stage.AmountLeft}, " +
                $"unspent={stage.UnspentTransactionCount}, spent={stage.SpentTransactionCount}, " +
                $"available={stage.Available}, canClaim={stage.CanClaim}, " +
                $"buttonMode={stage.ButtonMode}, date='{stage.CompletionDate}'");
        }

        // Verify header statistics
        manageVm.TotalStages.Should().Be(installmentCount,
            "ManageProject TotalStages header stat should match installment count");
        Log($"[STEP 10.0] Header stats: TotalInvestment={manageVm.TotalInvestment}, " +
            $"AvailableBalance={manageVm.AvailableBalance}, TotalStages={manageVm.TotalStages}, " +
            $"TransactionTotal={manageVm.TransactionTotal}, TransactionSpent={manageVm.TransactionSpent}, " +
            $"TransactionAvailable={manageVm.TransactionAvailable}");

        var claimableDeadline = DateTime.UtcNow + IndexerLagTimeout;
        var claimPollCount = 0;
        while (DateTime.UtcNow < claimableDeadline)
        {
            claimPollCount++;
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var stageSnapshot = string.Join(", ", manageVm.Stages.Select(s =>
                $"#{s.Number}:available={s.Available},canClaim={s.CanClaim},unspent={s.UnspentTransactionCount},spent={s.SpentTransactionCount},date='{s.CompletionDate}'"));
            Log($"[STEP 10] Stage snapshot poll #{claimPollCount}: {stageSnapshot}");

            var claimableStage = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.AvailableTransactions.Count > 0);
            if (claimableStage != null)
            {
                Log($"[STEP 10] Stage 1 is claimable after {claimPollCount} poll(s) with {claimableStage.AvailableTransactions.Count} transaction(s)");

                var selectedTransactions = claimableStage.AvailableTransactions.ToList();
                var claimResult = await manageVm.ClaimStageFundsAsync(claimableStage.Number, selectedTransactions);
                Dispatcher.UIThread.RunJobs();
                claimResult.Should().BeTrue("Founder should be able to spend stage 1");
                Log("[STEP 10] Founder successfully spent stage 1");
                break;
            }

            Log($"[STEP 10] Stage 1 not claimable yet (poll #{claimPollCount}). Retrying...");
            await Task.Delay(PollInterval);
        }

        var spentStageDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < spentStageDeadline)
        {
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var spentStageSnapshot = string.Join(", ", manageVm.Stages.Select(s =>
                $"#{s.Number}:available={s.Available},canClaim={s.CanClaim},unspent={s.UnspentTransactionCount},spent={s.SpentTransactionCount},date='{s.CompletionDate}'"));
            Log($"[STEP 10] Post-claim stage snapshot: {spentStageSnapshot}");

            if (manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0))
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        manageVm!.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0).Should().BeTrue(
            "Stage 1 should show spent transactions after founder claim");

        // STEP 11: Navigate to Funded → find our investment
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 11] Navigating to Funded section...");
        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Find our investment in the portfolio (it was added locally via AddToPortfolio)
        var investment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName ||
            i.ProjectIdentifier == foundProject.ProjectId);
        investment.Should().NotBeNull("Should find our investment in the portfolio");
        Log($"[STEP 11] Found investment: '{investment!.ProjectName}', status='{investment.StatusText}', step={investment.Step}");

        // Also try to reload from SDK (indexer may lag)
        Log("[STEP 11] Reloading investments from SDK...");
        await portfolioVm.LoadInvestmentsFromSdkAsync();
        Dispatcher.UIThread.RunJobs();
        Log($"[STEP 11] After SDK reload: {portfolioVm.Investments.Count} investment(s)");

        // Re-find after SDK reload (the list may have been replaced)
        var sdkInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName ||
            i.ProjectIdentifier == foundProject.ProjectId);

        // Use whichever we found — local or SDK-loaded
        var targetInvestment = sdkInvestment ?? investment;
        targetInvestment.Should().NotBeNull("Investment should exist in portfolio (local or SDK-loaded)");
        Log($"[STEP 11] Target investment: '{targetInvestment!.ProjectName}', identifier='{targetInvestment.ProjectIdentifier}', wallet='{targetInvestment.InvestmentWalletId}'");

        // ──────────────────────────────────────────────────────────────
        // STEP 12: Load recovery status and execute recovery
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 12] Loading recovery status...");

        // If the investment doesn't have a wallet ID yet (local-only add), set it from the wallet we used
        if (string.IsNullOrEmpty(targetInvestment.InvestmentWalletId))
        {
            targetInvestment.InvestmentWalletId = investWallet.Id.Value;
            Log($"[STEP 12] Set InvestmentWalletId to '{investWallet.Id.Value}' (was empty from local add)");
        }
        if (string.IsNullOrEmpty(targetInvestment.ProjectIdentifier))
        {
            targetInvestment.ProjectIdentifier = foundProject.ProjectId;
            Log($"[STEP 12] Set ProjectIdentifier to '{foundProject.ProjectId}' (was empty from local add)");
        }

        // Give the indexer an initial grace period before the first poll.
        // Signet mempool propagation can be slow — the mempool.space indexer needs
        // time to receive and index the just-broadcast investment transaction.
        Log("[STEP 12] Waiting 30s initial grace period for indexer to pick up the investment tx...");
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Wait for indexer to catch up with the investment transaction, then load recovery status
        bool hasRecoveryAction = false;
        var recoveryPollInterval = TimeSpan.FromSeconds(15); // less aggressive than PollInterval
        var recoveryDeadline = DateTime.UtcNow + IndexerLagTimeout;
        var pollCount = 0;
        while (DateTime.UtcNow < recoveryDeadline)
        {
            pollCount++;
            await portfolioVm.LoadRecoveryStatusAsync(targetInvestment);
            Dispatcher.UIThread.RunJobs();

            Log($"[STEP 12] Poll #{pollCount} — Recovery state: HasUnspent={targetInvestment.RecoveryState.HasUnspentItems}, " +
                $"InPenalty={targetInvestment.RecoveryState.HasSpendableItemsInPenalty}, " +
                $"HasReleaseSig={targetInvestment.RecoveryState.HasReleaseSignatures}, " +
                $"EndOfProject={targetInvestment.RecoveryState.EndOfProject}, " +
                $"AboveThreshold={targetInvestment.RecoveryState.IsAboveThreshold}, " +
                $"ActionKey={targetInvestment.RecoveryState.ActionKey}");

            if (targetInvestment.RecoveryState.HasAction)
            {
                hasRecoveryAction = true;
                break;
            }

            var remaining = recoveryDeadline - DateTime.UtcNow;
            Log($"[STEP 12] No recovery action available yet. Waiting for indexer... ({remaining.TotalSeconds:F0}s remaining)");
            await Task.Delay(recoveryPollInterval);
        }

        hasRecoveryAction.Should().BeTrue(
            $"Recovery state should have an available action after the investment transaction is indexed. " +
            $"Polled {pollCount} times over {IndexerLagTimeout.TotalMinutes} minutes. " +
            $"Project: {targetInvestment.ProjectIdentifier}, WalletId: {targetInvestment.InvestmentWalletId}");

        // ── Enhancement 3: Recovery stage display verification ──
        // After LoadRecoveryStatusAsync, the investment's Stages collection should be populated
        // with per-stage recovery data. Verify the stage count and statuses.
        Log("[STEP 12.1] Verifying recovery stage display...");
        targetInvestment.Stages.Count.Should().Be(installmentCount,
            $"Recovery should show {installmentCount} stages matching the project's payout schedule");

        foreach (var stage in targetInvestment.Stages)
        {
            stage.StageNumber.Should().BeGreaterThan(0, "Stage number should be positive");
            stage.Amount.Should().NotBe("0.00000000", $"Stage {stage.StageNumber} should have a non-zero amount");
            stage.Status.Should().BeOneOf("Released", "Pending", "Not Spent",
                $"Stage {stage.StageNumber} status should be a valid value");
            Log($"[STEP 12.1] Recovery Stage #{stage.StageNumber}: amount={stage.Amount}, status='{stage.Status}'");
        }

        // Stage 1 was spent by the founder — it should show as "Released"
        var recoveryStage1 = targetInvestment.Stages.FirstOrDefault(s => s.StageNumber == 1);
        recoveryStage1.Should().NotBeNull("Stage 1 should exist in recovery stages");
        recoveryStage1!.Status.Should().Be("Released",
            "Stage 1 should be 'Released' since the founder already spent it");

        // Remaining stages should be unspent (either "Pending" or "Not Spent")
        var unspentStages = targetInvestment.Stages.Where(s => s.StageNumber > 1).ToList();
        unspentStages.Should().AllSatisfy(s =>
            s.Status.Should().BeOneOf("Pending", "Not Spent",
                $"Stage {s.StageNumber} should be unspent since only stage 1 was spent"));

        // Verify computed recovery properties
        targetInvestment.StagesToRecover.Should().BeGreaterThan(0,
            "StagesToRecover should be positive since there are unreleased stages");
        double.TryParse(targetInvestment.AmountToRecover, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var amountToRecover).Should().BeTrue();
        amountToRecover.Should().BeGreaterThan(0,
            "AmountToRecover should be positive for unreleased stages");

        // Verify recovery button visibility
        targetInvestment.ShowRecoverButton.Should().BeTrue(
            "ShowRecoverButton should be true for an active investment with a recovery action available");
        targetInvestment.PenaltyButtonText.Should().NotBeNullOrEmpty(
            "PenaltyButtonText should have a label for the recovery action");
        Log($"[STEP 12.1] Recovery display verified. StagesToRecover={targetInvestment.StagesToRecover}, " +
            $"AmountToRecover={targetInvestment.AmountToRecover}, ButtonText='{targetInvestment.PenaltyButtonText}'");

        // Execute the appropriate recovery operation
        var actionKey = targetInvestment.RecoveryState.ActionKey;
        Log($"[STEP 12] Executing recovery action: '{actionKey}' ({targetInvestment.RecoveryState.ButtonLabel})...");

        bool recoveryResult;
        switch (actionKey)
        {
            case "recovery":
                recoveryResult = await portfolioVm.RecoverFundsAsync(targetInvestment);
                break;
            case "unfundedRelease":
                recoveryResult = await portfolioVm.ReleaseFundsAsync(targetInvestment);
                break;
            case "endOfProject":
                recoveryResult = await portfolioVm.ClaimEndOfProjectAsync(targetInvestment);
                break;
            case "penaltyRelease":
                recoveryResult = await portfolioVm.PenaltyReleaseFundsAsync(targetInvestment);
                break;
            default:
                recoveryResult = false;
                Log($"[STEP 12] ERROR: Unknown recovery action key: '{actionKey}'");
                break;
        }

        Dispatcher.UIThread.RunJobs();

        // ──────────────────────────────────────────────────────────────
        // STEP 13: Verify recovery succeeded
        // ──────────────────────────────────────────────────────────────
        Log($"[STEP 13] Recovery result: {recoveryResult}");
        recoveryResult.Should().BeTrue(
            $"Recovery operation '{actionKey}' should succeed (transaction built and published)");

        Log($"[STEP 13] Post-recovery state: HasUnspent={targetInvestment.RecoveryState.HasUnspentItems}, " +
            $"InPenalty={targetInvestment.RecoveryState.HasSpendableItemsInPenalty}, " +
            $"ActionKey={targetInvestment.RecoveryState.ActionKey}");

        // Cleanup: close window
        window.Close();
        Log("========== FullFundAndRecoverFlow PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private helper methods (reused from CreateProjectTest pattern)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to Settings, call ConfirmWipeData() on the ViewModel, then navigate back.
    /// </summary>
    private async Task WipeExistingData(Window window)
    {
        Log("  [Wipe] Navigating to Settings...");
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants()
            .OfType<SettingsView>()
            .FirstOrDefault();

        if (settingsView?.DataContext is SettingsViewModel settingsVm)
        {
            Log("  [Wipe] Found SettingsViewModel, calling ConfirmWipeData()...");
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
            Log("  [Wipe] Wipe completed.");
        }
        else
        {
            Log("  [Wipe] SettingsView/SettingsViewModel not found — skipping wipe.");
        }
    }

    /// <summary>
    /// Open the Create Wallet modal from the empty state, choose Generate,
    /// click Download Seed (gracefully skipped in headless), click Continue,
    /// and close on success.
    /// </summary>
    private async Task CreateWalletViaGenerate(Window window)
    {
        Log("  [Generate] Looking for Add Wallet button...");
        var addWalletBtn = FindAddWalletButton(window);
        addWalletBtn.Should().NotBeNull("should find the Add Wallet button in empty state");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        var choicePanel = await window.WaitForControl<StackPanel>("ChoicePanel", UiTimeout);
        choicePanel.Should().NotBeNull("Choice panel should be visible in create wallet modal");
        Log("  [Generate] ChoicePanel visible. Clicking 'Generate New'...");

        await window.ClickButton("BtnGenerate", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        var backupPanel = await window.WaitForControl<StackPanel>("BackupPanel", UiTimeout);
        backupPanel.Should().NotBeNull("Backup panel should be visible after clicking Generate New");
        Log("  [Generate] BackupPanel visible. Clicking 'Download Seed'...");

        await window.ClickButton("BtnDownloadSeed", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Log("  [Generate] Clicking 'Continue' to create wallet...");
        await window.ClickButton("BtnContinueBackup", UiTimeout);

        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet generation");
        Log("  [Generate] Wallet created successfully.");

        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var shellVm = window.GetShellViewModel();
        shellVm.IsModalOpen.Should().BeFalse("modal should be closed after clicking Done");
        Log("  [Generate] Modal closed. Wallet creation complete.");
    }

    /// <summary>
    /// Request testnet coins via the FundsViewModel's GetTestCoinsAsync and wait for balance.
    /// </summary>
    private async Task FundWalletViaFaucet(Window window)
    {
        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull("FundsViewModel should be available for faucet request");

        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty("Should have a wallet to fund");

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;
        var faucetAttempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (fundsVm.TotalBalance != "0.0000")
            {
                Log($"  [Faucet] Non-zero balance detected: {fundsVm.TotalBalance} (after {faucetAttempts} faucet attempt(s))");
                return;
            }

            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                faucetAttempts++;
                lastFaucetAttempt = DateTime.UtcNow;
                Log($"  [Faucet] Attempt #{faucetAttempts}: calling GetTestCoinsAsync...");

                (bool success, string? error) = await fundsVm.GetTestCoinsAsync(walletId!);
                Dispatcher.UIThread.RunJobs();

                if (success)
                    Log($"  [Faucet] Faucet request succeeded on attempt #{faucetAttempts}");
                else
                    Log($"  [Faucet] Faucet request failed: {error}. Retrying in {faucetRetryInterval.TotalSeconds}s");
            }

            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        fundsVm.TotalBalance.Should().NotBe("0.0000",
            $"Balance should become non-zero within {FaucetBalanceTimeout.TotalSeconds}s");
    }

    /// <summary>
    /// Open the create project wizard from MyProjectsView.
    /// </summary>
    private async Task OpenCreateWizard(Window window, MyProjectsViewModel myProjectsVm)
    {
        myProjectsVm.CreateProjectVm.ResetWizard();
        myProjectsVm.LaunchCreateWizard();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.ShowCreateWizard.Should().BeTrue("Create wizard should be visible");

        // Wire the deploy completed callback (normally done by MyProjectsView code-behind)
        myProjectsVm.CreateProjectVm.OnProjectDeployed = () =>
        {
            myProjectsVm.OnProjectDeployed(myProjectsVm.CreateProjectVm);
            myProjectsVm.CloseCreateWizard();
        };

        Log("  [Wizard] Create wizard opened and callback wired.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Utility methods
    // ═══════════════════════════════════════════════════════════════════

    private Button? FindAddWalletButton(Window window)
    {
        var buttons = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.IsVisible);

        foreach (var btn in buttons)
        {
            if (btn.Content is string text && text.Contains("Add Wallet"))
                return btn;

            if (btn.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.Text == "Add Wallet")
                        return btn;
                }
            }
        }

        return null;
    }

    private async Task ClickWalletCardButton(Window window, string automationId)
    {
        var button = await window.WaitForControl<Button>(automationId, UiTimeout);
        button.Should().NotBeNull($"WalletCard button '{automationId}' should be found");

        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
    }

    private FundsViewModel? GetFundsViewModel(Window window)
    {
        var fundsView = window.GetVisualDescendants()
            .OfType<FundsView>()
            .FirstOrDefault();
        return fundsView?.DataContext as FundsViewModel;
    }

    private MyProjectsViewModel? GetMyProjectsViewModel(Window window)
    {
        var myProjectsView = window.GetVisualDescendants()
            .OfType<MyProjectsView>()
            .FirstOrDefault();
        return myProjectsView?.DataContext as MyProjectsViewModel;
    }

    private FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        var findProjectsView = window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault();
        return findProjectsView?.DataContext as FindProjectsViewModel;
    }

    private FundersViewModel? GetFundersViewModel(Window window)
    {
        var fundersView = window.GetVisualDescendants()
            .OfType<FundersView>()
            .FirstOrDefault();
        return fundersView?.DataContext as FundersViewModel;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

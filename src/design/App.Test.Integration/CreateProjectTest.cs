using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using Angor.Shared;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
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
/// creates a wallet, funds it via testnet faucet, then creates an investment-type
/// project through the 6-step wizard and deploy flow.
///
/// Steps:
///   1. Wipe any existing data (via Settings → Danger Zone → Wipe Data)
///   2. Navigate to Funds → create wallet (Generate path)
///   3. Fund wallet via faucet, wait for non-zero balance
///   4. Navigate to My Projects → open create wizard
///   5. Walk through the 6-step wizard (type → profile → images → funding → stages → review)
///   6. Deploy via wallet payment (wallet selector → pay → success → go to my projects)
///   7. Verify the project appears in My Projects list (by unique GUID in description)
///   8. Reload from SDK and validate ProjectDto: name, description, type, target, stages,
///      dates, penalty, images, Nostr keys, and stage ratios/amounts
///
/// A unique GUID is embedded in the project description so we can precisely identify
/// the project we created, even if other projects exist in the list.
///
/// Random picsum.photos image URLs are set for both banner and profile images,
/// matching the pattern used by the CreateProjectViewModel defaults.
///
/// This test uses real testnet infrastructure (indexer + faucet API + Nostr relays)
/// and requires internet connectivity. It may take 60-180 seconds depending on
/// network conditions.
///
/// All UI interactions use AutomationProperties.AutomationId where available,
/// with ViewModel-direct calls for controls that use PointerPressed on Border
/// elements (type cards, wallet selector) or ListBox selections.
/// </summary>
public class CreateProjectTest
{
    /// <summary>
    /// Maximum time to wait for the faucet coins to appear in the wallet balance.
    /// This includes time for the faucet to replenish if temporarily out of funds.
    /// </summary>
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum time to wait for the deploy transaction to complete.
    /// </summary>
    private static readonly TimeSpan DeployTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Standard timeout for UI controls to appear after navigation or modal actions.
    /// </summary>
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Interval between balance refresh polls.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    [AvaloniaFact]
    public async Task FullCreateInvestmentProjectFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(CreateProjectTest));
        Log("========== STARTING FullCreateInvestmentProjectFlow ==========");

        // Generate a unique run ID so we can precisely identify *our* project
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Test Project {runId}";
        var projectAbout = $"Automated integration test run {runId}. Verifies end-to-end project creation.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        // Wizard input parameters — declared up front so validation can reference them
        // These values are deliberately chosen to ONLY pass in debug mode:
        //   - targetAmountBtc 0.0001 is below production minimum of 0.001 BTC
        //   - investEndDate = today fails production rule "must be after today"
        //   - penaltyDays = 0 fails production minimum of 10 days
        // With debug mode ON + testnet, these constraints are relaxed.
        var targetAmountBtc = "0.0001";
        var investEndDate = DateTime.Now.Date; // same day — debug only
        var penaltyDays = 0; // below production minimum of 10
        var durationValue = "6";
        var durationUnit = "Months";
        var releaseFrequency = "Monthly";
        var expectedStageCount = 6; // 6 months / 1 month frequency = 6 stages

        Log($"[STEP 0] Run ID: {runId}");
        Log($"[STEP 0] Project name: {projectName}");
        Log($"[STEP 0] Banner: {bannerImageUrl}");
        Log($"[STEP 0] Profile: {profileImageUrl}");

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

        Log("[STEP 1] Verifying shell header and funded state reset immediately after wipe...");
        shellVm.SelectedWallet.Should().BeNull("wipe data should clear the selected header wallet");
        shellVm.SwitcherWallets.Should().BeEmpty("wipe data should clear wallet switcher entries");
        shellVm.AvailableBalanceDisplay.Should().Be("0.0000 TBTC", "wipe data should reset the header available balance");
        shellVm.InvestedBalanceDisplay.Should().Be("0.0000 TBTC", "wipe data should reset the header invested balance");

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var portfolioVmAfterWipe = GetPortfolioViewModel(window);
        portfolioVmAfterWipe.Should().NotBeNull("PortfolioViewModel should be available after navigating to Funded");
        portfolioVmAfterWipe!.HasInvestments.Should().BeFalse("wipe data should clear funded investments without needing refresh");
        portfolioVmAfterWipe.Investments.Should().BeEmpty("Funded list should be empty immediately after wipe");

        // Enable debug mode AFTER wipe (wipe resets settings to defaults).
        // Debug mode only relaxes validation constraints when the network is also testnet (not mainnet).
        var networkConfig = global::App.App.Services.GetRequiredService<INetworkConfiguration>();
        networkConfig.SetDebugMode(true);
        Log("[STEP 1b] Debug mode ENABLED via INetworkConfiguration (after wipe).");

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

        Log("[STEP 3] Verifying header wallet balance sync and that Funded remains empty...");
        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull("FundsViewModel should still be available after funding");

        var headerUpdated = await TestHelpers.WaitForCondition(
            () => shellVm.SelectedWallet != null && shellVm.AvailableBalanceDisplay != "0.0000 TBTC",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(250));
        headerUpdated.Should().BeTrue("header wallet balance should update automatically after funding");

        shellVm.SelectedWallet.Should().NotBeNull("header should auto-select the funded wallet");
        shellVm.AvailableBalanceDisplay.Should().Be(
            fundsVm!.TotalBalance + " TBTC",
            "header available balance should match the confirmed Funds total balance");

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var portfolioVmBeforeCreate = GetPortfolioViewModel(window);
        portfolioVmBeforeCreate.Should().NotBeNull("PortfolioViewModel should be available before project creation");
        portfolioVmBeforeCreate!.HasInvestments.Should().BeFalse("wallet funding alone should not create funded investments");
        portfolioVmBeforeCreate.Investments.Should().BeEmpty("Funded should remain empty before any investment flow happens");

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Navigate to My Projects → open create wizard
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 4] Navigating to My Projects section...");
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // My Projects should show empty state (no projects yet)
        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull("MyProjectsViewModel should be available");
        Log($"[STEP 4] HasProjects: {myProjectsVm!.HasProjects}");

        // Open the create wizard via the ViewModel
        Log("[STEP 4] Opening create wizard...");
        await OpenCreateWizard(window, myProjectsVm);

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Walk through the 6-step wizard
        // ──────────────────────────────────────────────────────────────

        var wizardVm = myProjectsVm.CreateProjectVm;
        wizardVm.Should().NotBeNull("CreateProjectViewModel should exist");

        // ── Step 1: Dismiss welcome, select "investment" type ──
        Log("[STEP 5.1] Dismissing welcome screen...");
        wizardVm.ShowWelcome.Should().BeTrue("Wizard should start with welcome screen");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);

        Log("[STEP 5.1] Selecting 'investment' project type...");
        wizardVm.SelectProjectType("investment");
        Dispatcher.UIThread.RunJobs();
        wizardVm.IsInvestment.Should().BeTrue();

        Log("[STEP 5.1] Advancing to Step 2...");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();
        wizardVm.CurrentStep.Should().Be(2, "Should advance to step 2 after selecting type");

        // ── Step 2: Project profile — name and about ──
        Log("[STEP 5.2] Filling project name and about...");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 5.2] Advancing to Step 3...");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();
        wizardVm.CurrentStep.Should().Be(3, "Should advance to step 3 after filling profile");

        // ── Step 3: Project images — set random picsum.photos URLs ──
        Log("[STEP 5.3] Setting banner and profile image URLs...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        Dispatcher.UIThread.RunJobs();
        wizardVm.BannerUrl.Should().Be(bannerImageUrl);
        wizardVm.ProfileUrl.Should().Be(profileImageUrl);
        Log($"[STEP 5.3] BannerUrl: {wizardVm.BannerUrl}");
        Log($"[STEP 5.3] ProfileUrl: {wizardVm.ProfileUrl}");

        Log("[STEP 5.3] Advancing to Step 4...");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();
        wizardVm.CurrentStep.Should().Be(4, "Should advance to step 4 after setting images");

        // ── Step 4: Funding configuration — target amount + end date + penalty ──
        Log("[STEP 5.4] Setting target amount, end date, and penalty days...");
        wizardVm.TargetAmount = targetAmountBtc;
        wizardVm.InvestEndDate = investEndDate;
        wizardVm.PenaltyDays = penaltyDays;
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 5.4] Advancing to Step 5...");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();
        wizardVm.CurrentStep.Should().Be(5, "Should advance to step 5 after filling funding config");

        // ── Step 5: Stages — dismiss welcome, set duration + frequency, generate stages ──
        Log("[STEP 5.5] Dismissing Step 5 welcome interstitial...");
        wizardVm.ShowStep5Welcome.Should().BeTrue("Step 5 should start with welcome screen");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);

        Log("[STEP 5.5] Setting duration to 6 months, frequency to Monthly...");
        wizardVm.DurationValue = durationValue;
        wizardVm.DurationUnit = durationUnit;
        wizardVm.ReleaseFrequency = releaseFrequency;
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 5.5] Generating investment stages...");
        wizardVm.GenerateInvestmentStages();
        Dispatcher.UIThread.RunJobs();
        wizardVm.Stages.Count.Should().Be(expectedStageCount, $"Should have generated exactly {expectedStageCount} stages");
        Log($"[STEP 5.5] Generated {wizardVm.Stages.Count} stages");

        // Validate each generated stage before deploy
        var totalPercentage = 0.0;
        for (int i = 0; i < wizardVm.Stages.Count; i++)
        {
            var stage = wizardVm.Stages[i];
            stage.StageNumber.Should().Be(i + 1, $"Stage {i + 1} should have correct StageNumber");
            stage.StageLabel.Should().Be("Stage", "Investment type stages should be labeled 'Stage'");
            stage.AmountBtc.Should().NotBeNullOrEmpty($"Stage {i + 1} should have an AmountBtc");
            stage.Percentage.Should().NotBeNullOrEmpty($"Stage {i + 1} should have a Percentage");
            stage.ReleaseDate.Should().NotBeNullOrEmpty($"Stage {i + 1} should have a ReleaseDate");

            // Parse and validate percentage
            var pctStr = stage.Percentage.Replace("%", "");
            double.TryParse(pctStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)
                .Should().BeTrue($"Stage {i + 1} percentage '{stage.Percentage}' should be parseable");
            pct.Should().BeGreaterThan(0, $"Stage {i + 1} percentage should be positive");
            totalPercentage += pct;

            Log($"[STEP 5.5]   Stage {stage.StageNumber}: {stage.Percentage} ({stage.AmountBtc} BTC) on {stage.ReleaseDate}");
        }
        totalPercentage.Should().BeApproximately(100.0, 1.0, "Total stage percentages should sum to ~100%");

        Log("[STEP 5.5] Advancing to Step 6 (Review & Deploy)...");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();
        wizardVm.CurrentStep.Should().Be(6, "Should advance to step 6 after generating stages");

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Deploy the project
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 6] Starting deploy flow...");

        // Set the password provider key so the SDK can decrypt the wallet
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log("[STEP 6] Set SimplePasswordProvider key to 'default-key'.");

        // Click the Deploy button (triggers Deploy() which shows DeployFlowOverlay as shell modal)
        Log("[STEP 6] Calling Deploy()...");
        wizardVm.Deploy();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000); // Allow LoadWalletsAsync to complete
        Dispatcher.UIThread.RunJobs();

        var deployVm = wizardVm.DeployFlow;
        deployVm.IsVisible.Should().BeTrue("Deploy overlay should be visible after Deploy()");
        deployVm.CurrentScreen.Should().Be(DeployScreen.WalletSelector, "Should start at wallet selector");

        // Wait for wallets to load
        Log("[STEP 6] Waiting for wallets to load...");
        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline && deployVm.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
        deployVm.Wallets.Count.Should().BeGreaterThan(0, "At least one wallet should be loaded");
        Log($"[STEP 6] Loaded {deployVm.Wallets.Count} wallet(s)");

        // Select the first wallet via ViewModel (wallet cards use PointerPressed on Border)
        var wallet = deployVm.Wallets[0];
        Log($"[STEP 6] Selecting wallet: {wallet.Name} (balance: {wallet.Balance})...");
        deployVm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();
        deployVm.SelectedWallet.Should().NotBeNull("Should have a selected wallet");

        // Click "Pay with Wallet" — this triggers the real SDK deploy pipeline
        Log("[STEP 6] Paying with wallet (SDK deploy pipeline)...");
        deployVm.PayWithWallet();

        // Wait for deploy to complete (or fail) — poll for Success screen
        var deployDeadline = DateTime.UtcNow + DeployTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (deployVm.CurrentScreen == DeployScreen.Success)
            {
                Log("[STEP 6] Deploy succeeded! Success screen visible.");
                break;
            }

            if (!deployVm.IsDeploying && deployVm.CurrentScreen != DeployScreen.Success)
            {
                // Deploying finished but not on success screen — check for error
                Log($"[STEP 6] Deploy status: {deployVm.DeployStatusText}");
                if (deployVm.DeployStatusText.Contains("Failed") || deployVm.DeployStatusText.Contains("error"))
                {
                    Log($"[STEP 6] Deploy ERROR: {deployVm.DeployStatusText}");
                    break;
                }
            }

            await Task.Delay(PollInterval);
        }

        deployVm.CurrentScreen.Should().Be(DeployScreen.Success,
            $"Deploy should reach success screen. Last status: {deployVm.DeployStatusText}");

        // Click "Go to My Projects" — closes modal, adds project to list
        Log("[STEP 6] Clicking 'Go to My Projects'...");
        deployVm.GoToMyProjects();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Also hide the shell modal (the code-behind does this, but in headless
        // we're calling VM directly so the shell modal may still be open)
        if (shellVm.IsModalOpen)
        {
            shellVm.HideModal();
            Dispatcher.UIThread.RunJobs();
        }

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Verify the project appears in My Projects list
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 7] Verifying project appears in My Projects list...");

        // 7a. Wizard should be closed and local project list should be populated
        myProjectsVm.ShowCreateWizard.Should().BeFalse("Wizard should be closed after deploy");
        myProjectsVm.HasProjects.Should().BeTrue("Should have at least one project after deploy");

        // 7b. Find our exact project by the unique GUID embedded in the description
        var project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull($"Project with run ID '{runId}' should appear in the project list");
        Log($"[STEP 7] Found project: Name='{project!.Name}', Type='{project.ProjectType}', Status='{project.Status}'");

        // 7c. Validate local project item fields from OnProjectDeployed()
        project.Name.Should().Be(projectName, "Project name should match wizard input");
        project.Description.Should().Be(projectAbout, "Project description should match wizard input");
        project.ProjectType.Should().Be("investment", "Project type should be 'investment'");
        project.TargetAmount.Should().Be(targetAmountBtc, "Target amount should match wizard input");
        project.Status.Should().Be("Open", "Newly deployed project should have 'Open' status");
        project.BannerUrl.Should().Be(bannerImageUrl, "Banner URL should match wizard input");
        project.LogoUrl.Should().Be(profileImageUrl, "Logo/profile URL should match wizard input");
        project.StartDate.Should().NotBeNullOrEmpty("Start date should be set");
        Log($"[STEP 7] Local validation passed: name, description (GUID), type, target, status, images, start date");

        // ──────────────────────────────────────────────────────────────
        // STEP 8: Reload from SDK and validate ProjectDto fields
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 8] Fetching project from SDK via GetFounderProjects...");

        // Reload founder projects from SDK (this fetches from Nostr/indexer)
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.HasProjects.Should().BeTrue("SDK should return at least one founder project");

        // Find our project again by GUID (SDK reload replaces the local list)
        var sdkProject = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        sdkProject.Should().NotBeNull($"SDK-loaded project with run ID '{runId}' should be found");
        Log($"[STEP 8] SDK project found: Name='{sdkProject!.Name}', Identifier='{sdkProject.ProjectIdentifier}'");

        // 8a. Validate SDK-loaded fields
        sdkProject.Name.Should().Be(projectName, "SDK project name should match");
        sdkProject.Description.Should().Contain(runId, "SDK project description should contain our run ID");
        sdkProject.ProjectType.Should().Be("investment", "SDK project type should be 'investment'");
        sdkProject.ProjectIdentifier.Should().NotBeNullOrEmpty("SDK project should have a ProjectIdentifier");
        sdkProject.OwnerWalletId.Should().NotBeNullOrEmpty("SDK project should have an OwnerWalletId");
        sdkProject.BannerUrl.Should().Contain("picsum.photos", "SDK banner should be our picsum URL");
        sdkProject.LogoUrl.Should().Contain("picsum.photos", "SDK logo should be our picsum URL");
        sdkProject.StartDate.Should().NotBeNullOrEmpty("SDK project should have a start date");
        Log($"[STEP 8] SDK fields validated: identifier, wallet, images, start date");

        // 8b. Parse target amount — SDK returns as formatted BTC string (F5)
        double.TryParse(sdkProject.TargetAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out var sdkTargetBtc)
            .Should().BeTrue("SDK target amount should be parseable as double");
        sdkTargetBtc.Should().BeApproximately(
            double.Parse(targetAmountBtc, CultureInfo.InvariantCulture), 0.001,
            "SDK target amount should match the wizard input");
        Log($"[STEP 8] Target amount: {sdkTargetBtc} BTC (expected: {targetAmountBtc})");

        // 8c. Fetch the full ProjectDto from IProjectAppService to validate stages
        Log("[STEP 8] Fetching full ProjectDto from SDK for stage validation...");
        var projectAppService = global::App.App.Services.GetRequiredService<IProjectAppService>();
        var walletAppService = global::App.App.Services.GetRequiredService<Angor.Sdk.Wallet.Application.IWalletAppService>();

        var metadatas = await walletAppService.GetMetadatas();
        metadatas.IsSuccess.Should().BeTrue("Should be able to get wallet metadatas");

        // Find our project's ProjectDto via GetFounderProjects
        Angor.Sdk.Funding.Projects.Dtos.ProjectDto? projectDto = null;
        foreach (var meta in metadatas.Value)
        {
            var founderResult = await projectAppService.GetFounderProjects(meta.Id);
            if (founderResult.IsFailure) continue;

            projectDto = founderResult.Value.Projects
                .FirstOrDefault(p => p.ShortDescription != null && p.ShortDescription.Contains(runId));
            if (projectDto != null) break;
        }

        projectDto.Should().NotBeNull($"Should find ProjectDto with run ID '{runId}' via SDK");
        Log($"[STEP 8] ProjectDto found: Id='{projectDto!.Id}', Stages={projectDto.Stages?.Count ?? 0}");

        // 8d. Validate ProjectDto core fields
        projectDto.Name.Should().Be(projectName);
        projectDto.ShortDescription.Should().Contain(runId);
        projectDto.ProjectType.Should().Be(Angor.Shared.Models.ProjectType.Invest);
        projectDto.TargetAmount.Should().Be(10_000L, "0.0001 BTC = 10,000 sats");
        projectDto.Banner.Should().NotBeNull("Banner URI should be set");
        projectDto.Banner!.ToString().Should().Contain("picsum.photos");
        projectDto.Avatar.Should().NotBeNull("Avatar URI should be set");
        projectDto.Avatar!.ToString().Should().Contain("picsum.photos");
        projectDto.NostrNpubKeyHex.Should().NotBeNullOrEmpty("Should have a Nostr pub key");
        projectDto.Version.Should().BeGreaterThanOrEqualTo(2, "Should be version 2+");
        Log("[STEP 8] ProjectDto core fields validated");

        // 8e. Validate stages from SDK
        projectDto.Stages.Should().NotBeNull("ProjectDto should have stages");
        projectDto.Stages.Should().HaveCount(expectedStageCount,
            $"ProjectDto should have exactly {expectedStageCount} stages (6 months / monthly)");

        var totalRatio = 0m;
        for (int i = 0; i < projectDto.Stages.Count; i++)
        {
            var stageDto = projectDto.Stages[i];
            stageDto.Index.Should().Be(i, $"Stage {i} index should match position");
            stageDto.ReleaseDate.Should().BeAfter(DateTime.UtcNow, $"Stage {i} release date should be in the future");
            stageDto.RatioOfTotal.Should().BeGreaterThan(0, $"Stage {i} should have a positive ratio");

            totalRatio += stageDto.RatioOfTotal;

            Log($"[STEP 8]   Stage {stageDto.Index}: {stageDto.RatioOfTotal:P1} on {stageDto.ReleaseDate:yyyy-MM-dd}");
        }

        totalRatio.Should().BeApproximately(1.0m, 0.01m, "Stage ratios should sum to ~100%");
        Log($"[STEP 8] Stage totals: ratio={totalRatio:P1}");

        // 8f. Validate funding dates
        projectDto.FundingStartDate.Should().BeBefore(investEndDate.ToUniversalTime().AddDays(1),
            "Funding start date should be before end date");
        projectDto.FundingEndDate.Date.Should().BeCloseTo(investEndDate, TimeSpan.FromDays(2),
            "Funding end date should be close to the wizard input end date");
        Log($"[STEP 8] Dates: start={projectDto.FundingStartDate:yyyy-MM-dd}, end={projectDto.FundingEndDate:yyyy-MM-dd}");

        // 8g. Validate penalty configuration
        projectDto.PenaltyDuration.TotalDays.Should().BeApproximately(0, 1,
            "Penalty duration should be ~0 days (debug mode value)");
        Log($"[STEP 8] Penalty duration: {projectDto.PenaltyDuration.TotalDays} days");

        // Cleanup: close window
        window.Close();
        Log("========== FullCreateInvestmentProjectFlow PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private helper methods for each test step
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
    /// If the faucet is temporarily out of funds, retries the faucet call with backoff
    /// (every 30s) while also polling for balance in between, up to the total timeout.
    /// </summary>
    private async Task FundWalletViaFaucet(Window window)
    {
        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull("FundsViewModel should be available for faucet request");

        // Get the wallet ID from the first wallet in SeedGroups
        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty("Should have a wallet to fund");

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;
        var faucetAttempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Check if balance is already non-zero
            if (fundsVm.TotalBalance != "0.0000")
            {
                Log($"  [Faucet] Non-zero balance detected: {fundsVm.TotalBalance} (after {faucetAttempts} faucet attempt(s))");
                return;
            }

            // Time to (re-)request from faucet?
            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                faucetAttempts++;
                lastFaucetAttempt = DateTime.UtcNow;
                Log($"  [Faucet] Attempt #{faucetAttempts}: calling GetTestCoinsAsync...");

                (bool success, string? error) = await fundsVm.GetTestCoinsAsync(walletId!);
                Dispatcher.UIThread.RunJobs();

                if (success)
                {
                    Log($"  [Faucet] Faucet request succeeded on attempt #{faucetAttempts}");
                }
                else
                {
                    var remaining = deadline - DateTime.UtcNow;
                    Log($"  [Faucet] Faucet request failed: {error}. Retrying in {faucetRetryInterval.TotalSeconds}s ({remaining.TotalSeconds:F0}s remaining)");
                }
            }

            // Poll balance via refresh
            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        fundsVm.TotalBalance.Should().NotBe("0.0000",
            $"Balance should become non-zero within {FaucetBalanceTimeout.TotalSeconds}s. Faucet was attempted {faucetAttempts} time(s).");
    }

    /// <summary>
    /// Open the create project wizard from MyProjectsView.
    /// Calls the ViewModel method directly since the EmptyState button uses PointerPressed.
    /// </summary>
    private async Task OpenCreateWizard(Window window, MyProjectsViewModel myProjectsVm)
    {
        // Reset wizard and open it
        myProjectsVm.CreateProjectVm.ResetWizard();
        myProjectsVm.LaunchCreateWizard();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.ShowCreateWizard.Should().BeTrue("Create wizard should be visible");

        // Wire the deploy completed callback (normally done by MyProjectsView.OpenCreateWizard code-behind)
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

    /// <summary>
    /// Find the "Add Wallet" button in the EmptyState control.
    /// </summary>
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

    /// <summary>
    /// Click a button on the WalletCard by its AutomationId.
    /// </summary>
    private async Task ClickWalletCardButton(Window window, string automationId)
    {
        var button = await window.WaitForControl<Button>(automationId, UiTimeout);
        button.Should().NotBeNull($"WalletCard button '{automationId}' should be found");

        Log($"  [Click] Clicking '{automationId}'...");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Get the FundsViewModel from the current Funds view in the visual tree.
    /// </summary>
    private FundsViewModel? GetFundsViewModel(Window window)
    {
        var fundsView = window.GetVisualDescendants()
            .OfType<FundsView>()
            .FirstOrDefault();
        return fundsView?.DataContext as FundsViewModel;
    }

    /// <summary>
    /// Get the MyProjectsViewModel from the current My Projects view.
    /// </summary>
    private MyProjectsViewModel? GetMyProjectsViewModel(Window window)
    {
        var myProjectsView = window.GetVisualDescendants()
            .OfType<MyProjectsView>()
            .FirstOrDefault();
        return myProjectsView?.DataContext as MyProjectsViewModel;
    }

    /// <summary>
    /// Get the PortfolioViewModel from the current Funded view.
    /// </summary>
    private PortfolioViewModel? GetPortfolioViewModel(Window window)
    {
        var portfolioView = window.GetVisualDescendants()
            .OfType<PortfolioView>()
            .FirstOrDefault();
        return portfolioView?.DataContext as PortfolioViewModel;
    }

    /// <summary>
    /// Write a timestamped log message to the console.
    /// Visible in test output when running with --logger "console;verbosity=detailed".
    /// </summary>
    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using App.Test.Integration.Helpers;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// End-to-end integration test that validates wallet import from seed words
/// and the ScanFounderProjects flow. This is the most critical recovery path
/// a user needs after device loss.
///
/// Flow:
///   Profile A (Creator):
///     1. Wipe data, create wallet via Generate, fund via faucet
///     2. Create and deploy a fund project
///     3. Record the seed words and project identifier
///
///   Profile B (Importer):
///     4. Wipe data, import wallet using the same seed words
///     5. Verify wallet ID is identical (deterministic from xpub hash)
///     6. Verify balance is discoverable from on-chain data
///     7. Trigger ScanFounderProjects
///     8. Verify the deployed project appears in My Projects
///
/// This validates:
///   - Mnemonic import produces the same deterministic wallet ID
///   - Derived project keys are correctly regenerated for the network
///   - ScanFounderProjects correctly queries the indexer for all 15 key slots
///   - Local DB (FounderProjectsDocument) is populated from scan results
///   - Balance refresh discovers existing UTXOs after import
///
/// Uses real testnet infrastructure (indexer + faucet + Nostr relays).
/// May take 120-300 seconds depending on network conditions.
/// </summary>
public class WalletImportAndProjectScanTest
{
    private const string TestName = "WalletImportAndProjectScan";
    private const string CreatorProfile = TestName + "-Creator";
    private const string ImporterProfile = TestName + "-Importer";


    /// <summary>
    /// Carries state between profile windows: seed words, wallet ID, project details.
    /// </summary>
    private sealed record CreatorState(
        string SeedWords,
        string WalletId,
        string RunId,
        string ProjectName,
        string ProjectIdentifier);

    [AvaloniaFact]
    public async Task ImportWalletAndScanFounderProjects()
    {
        var initializedProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Import Scan {runId}";
        var projectAbout = $"{TestName} run {runId}. Validates wallet import from seed and project scan.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var payoutDay = DateTime.UtcNow.DayOfWeek.ToString();

        Log(null, $"========== STARTING {nameof(ImportWalletAndScanFounderProjects)} ==========");
        Log(null, $"Run ID: {runId}");
        Log(null, $"Creator profile: {CreatorProfile}");
        Log(null, $"Importer profile: {ImporterProfile}");

        CreatorState? creatorState = null;

        // ──────────────────────────────────────────────────────────────
        // PHASE 1: Creator — generate wallet, fund it, deploy a project
        // ──────────────────────────────────────────────────────────────
        await WithProfileWindow(CreatorProfile, initializedProfiles, async window =>
        {
            creatorState = await CreateWalletFundAndDeployAsync(
                window,
                CreatorProfile,
                projectName,
                projectAbout,
                bannerImageUrl,
                profileImageUrl,
                payoutDay,
                runId);
        });

        creatorState.Should().NotBeNull("Creator phase should produce state with seed words and project info");
        Log(null, $"Creator phase complete. WalletId={creatorState!.WalletId}, ProjectId={creatorState.ProjectIdentifier}");
        Log(null, $"Seed words recorded ({creatorState.SeedWords.Split(' ').Length} words)");

        // ──────────────────────────────────────────────────────────────
        // PHASE 2: Importer — import wallet from same seed, scan projects
        // ──────────────────────────────────────────────────────────────
        await WithProfileWindow(ImporterProfile, initializedProfiles, async window =>
        {
            await ImportWalletAndVerifyProjectScanAsync(window, ImporterProfile, creatorState);
        });

        Log(null, $"========== {nameof(ImportWalletAndScanFounderProjects)} PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 1: Creator — generate, fund, deploy
    // ═══════════════════════════════════════════════════════════════════

    private async Task<CreatorState> CreateWalletFundAndDeployAsync(
        Window window,
        string profileName,
        string projectName,
        string projectAbout,
        string bannerImageUrl,
        string profileImageUrl,
        string payoutDay,
        string runId)
    {
        // ── Step 1: Create wallet and capture seed words ──
        await window.NavigateToSectionAndVerify("Funds");

        Log(profileName, "Creating wallet via Generate flow and capturing seed words...");
        var seedWords = await CreateWalletAndCaptureSeed(window, profileName);
        seedWords.Should().NotBeNullOrEmpty("Seed words should be captured during wallet generation");
        Log(profileName, $"Seed words captured: {seedWords.Split(' ').Length} words");

        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        // ── Step 2: Get the wallet ID ──
        var fundsVm = window.GetFundsViewModel();
        fundsVm.Should().NotBeNull();

        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty("Wallet should have an ID after creation");
        Log(profileName, $"Creator wallet ID: {walletId}");

        // ── Step 3: Fund via faucet ──
        Log(profileName, "Funding wallet via faucet...");
        await FundWalletViaFaucet(window, profileName);

        // ── Step 4: Create and deploy a project ──
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = window.GetMyProjectsViewModel();
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!, profileName);

        var wizardVm = myProjectsVm!.CreateProjectVm;
        wizardVm.Should().NotBeNull();

        // Step 4.1: Select fund type
        Log(profileName, "Selecting Fund project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("fund");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4.2: Project profile
        Log(profileName, $"Setting project metadata: {projectName}");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4.3: Images
        Log(profileName, "Setting project images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4.4: Funding config
        Log(profileName, "Configuring target amount and threshold...");
        wizardVm.TargetAmount = "1.0";
        wizardVm.ApprovalThreshold = "0.01";
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4.5: Payout schedule
        Log(profileName, "Configuring payout schedule...");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.PayoutFrequency = "Weekly";
        wizardVm.ToggleInstallmentCount(3);
        wizardVm.WeeklyPayoutDay = payoutDay;
        wizardVm.GeneratePayoutSchedule();
        wizardVm.Stages.Count.Should().Be(3);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        // Step 4.6: Deploy
        Log(profileName, "Deploying project...");
        wizardVm.Deploy();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();

        var deployVm = wizardVm.DeployFlow;
        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline && deployVm.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }

        deployVm.Wallets.Count.Should().BeGreaterThan(0);
        deployVm.SelectWallet(deployVm.Wallets[0]);
        Dispatcher.UIThread.RunJobs();
        deployVm.PayWithWallet();

        var deployDeadline = DateTime.UtcNow + TestHelpers.TransactionTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (deployVm.CurrentScreen == DeployScreen.Success)
            {
                break;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        deployVm.CurrentScreen.Should().Be(DeployScreen.Success,
            $"Deploy should reach success. Last status: {deployVm.DeployStatusText}");

        deployVm.GoToMyProjects();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // ── Step 5: Get the project identifier ──
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var project = myProjectsVm.Projects.FirstOrDefault(p =>
            p.Description.Contains(runId, StringComparison.Ordinal));
        project.Should().NotBeNull($"Project with run ID '{runId}' should appear in My Projects");
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty("Project should have an identifier after founder reload");
        project.Name.Should().Be(projectName);
        Log(profileName, $"Project deployed. ProjectId={project.ProjectIdentifier}, Name={project.Name}");

        return new CreatorState(seedWords, walletId!, runId, projectName, project.ProjectIdentifier!);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 2: Importer — import from seed, verify wallet ID, scan
    // ═══════════════════════════════════════════════════════════════════

    private async Task ImportWalletAndVerifyProjectScanAsync(
        Window window,
        string profileName,
        CreatorState creatorState)
    {
        // ── Step 1: Import wallet from seed words ──
        await window.NavigateToSectionAndVerify("Funds");

        Log(profileName, "Importing wallet from creator's seed words...");
        await ImportWalletViaSeedWords(window, profileName, creatorState.SeedWords);

        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull("WalletCard should appear after wallet import");

        // ── Step 2: Verify wallet ID is identical ──
        var fundsVm = window.GetFundsViewModel();
        fundsVm.Should().NotBeNull();

        var importedWalletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        importedWalletId.Should().NotBeNullOrEmpty("Imported wallet should have an ID");
        importedWalletId.Should().Be(creatorState.WalletId,
            "Imported wallet should have the same deterministic wallet ID (derived from xpub hash)");
        Log(profileName, $"Wallet ID match confirmed: {importedWalletId}");

        // ── Step 3: Wait for balance to appear (on-chain UTXO discovery) ──
        Log(profileName, "Waiting for balance to appear from on-chain UTXO discovery...");
        var balanceDeadline = DateTime.UtcNow + TestHelpers.FaucetBalanceTimeout;
        while (DateTime.UtcNow < balanceDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (fundsVm.TotalBalance != "0.0000")
            {
                Log(profileName, $"Balance discovered: {fundsVm.TotalBalance}");
                break;
            }

            await window.ClickWalletCardButton( "WalletCardBtnRefresh");
            await Task.Delay(TestHelpers.PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        fundsVm.TotalBalance.Should().NotBe("0.0000",
            "Imported wallet should discover on-chain UTXOs and show non-zero balance");

        // ── Step 4: Navigate to My Projects — should be empty before scan ──
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = window.GetMyProjectsViewModel();
        myProjectsVm.Should().NotBeNull();

        // Load founder projects from local DB — should be empty since this is a fresh profile
        await myProjectsVm!.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();
        Log(profileName, $"Projects before scan: {myProjectsVm.Projects.Count}");

        // ── Step 5: Scan for founder projects ──
        Log(profileName, "Scanning for founder projects (ScanFounderProjects)...");
        await myProjectsVm.ScanForProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        // Wait for the scan results to populate — the indexer may take time
        MyProjectItemViewModel? scannedProject = null;
        var scanDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < scanDeadline)
        {
            await myProjectsVm.ScanForProjectsAsync();
            Dispatcher.UIThread.RunJobs();

            scannedProject = myProjectsVm.Projects.FirstOrDefault(p =>
                string.Equals(p.ProjectIdentifier, creatorState.ProjectIdentifier, StringComparison.Ordinal) ||
                p.Description.Contains(creatorState.RunId, StringComparison.Ordinal));

            if (scannedProject != null)
            {
                break;
            }

            Log(profileName, $"Project not found in scan yet ({myProjectsVm.Projects.Count} project(s)). Retrying...");
            await Task.Delay(TestHelpers.PollInterval);
        }

        // ── Step 6: Verify the scanned project matches ──
        scannedProject.Should().NotBeNull(
            $"ScanFounderProjects should discover the project deployed by the creator (run ID: {creatorState.RunId})");
        scannedProject!.Name.Should().Be(creatorState.ProjectName,
            "Scanned project name should match the original");
        scannedProject.ProjectIdentifier.Should().Be(creatorState.ProjectIdentifier,
            "Scanned project identifier should match the original");

        Log(profileName, $"Project scan successful! Found: '{scannedProject.Name}' (ID: {scannedProject.ProjectIdentifier})");
        Log(profileName, "Wallet import + project scan test completed successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wallet helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a wallet via the Generate flow, but capture the seed words
    /// from the SeedPhraseDisplay before confirming.
    /// </summary>
    private async Task<string> CreateWalletAndCaptureSeed(Window window, string profileName)
    {
        var addWalletBtn = window.FindAddWalletButton();
        addWalletBtn.Should().NotBeNull("Should find the Add Wallet button");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // Click Generate New
        await window.ClickButton("BtnGenerate", TestHelpers.UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        // BackupPanel should now be visible with the seed phrase
        var backupPanel = await window.WaitForControl<StackPanel>("BackupPanel", TestHelpers.UiTimeout);
        backupPanel.Should().NotBeNull("Backup panel should be visible after clicking Generate New");

        // Capture seed words from the SeedPhraseDisplay TextBlock
        var seedDisplay = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Name == "SeedPhraseDisplay");
        seedDisplay.Should().NotBeNull("SeedPhraseDisplay TextBlock should exist in BackupPanel");
        var seedWords = seedDisplay!.Text ?? "";
        seedWords.Should().NotBeNullOrEmpty("Seed phrase should be displayed");
        seedWords.Split(' ').Length.Should().BeOneOf(new[] { 12, 24 }, "Seed phrase should be 12 or 24 words");
        Log(profileName, $"Captured seed phrase ({seedWords.Split(' ').Length} words)");

        // Click Download Seed (skipped in headless but enables Continue)
        await window.ClickButton("BtnDownloadSeed", TestHelpers.UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // Click Continue to create wallet
        await window.ClickButton("BtnContinueBackup", TestHelpers.UiTimeout);

        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet generation");

        await window.ClickButton("BtnCreateWalletDone", TestHelpers.UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Wallet created and seed words captured.");
        return seedWords;
    }

    /// <summary>
    /// Import a wallet by entering seed words into the Import modal flow.
    /// Uses the CreateWalletModal's Import path: ChoicePanel → ImportPanel → SuccessPanel.
    /// </summary>
    private async Task ImportWalletViaSeedWords(Window window, string profileName, string seedWords)
    {
        var addWalletBtn = window.FindAddWalletButton();
        addWalletBtn.Should().NotBeNull("Should find the Add Wallet button for import");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // Click Import
        await window.ClickButton("BtnImport", TestHelpers.UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        // ImportPanel should now be visible
        var importPanel = await window.WaitForControl<StackPanel>("ImportPanel", TestHelpers.UiTimeout);
        importPanel.Should().NotBeNull("Import panel should be visible after clicking Import");

        // Enter seed words into the SeedPhraseInput TextBox
        var seedInput = window.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(tb => tb.Name == "SeedPhraseInput");
        seedInput.Should().NotBeNull("SeedPhraseInput TextBox should exist in ImportPanel");
        seedInput!.Text = seedWords;
        Dispatcher.UIThread.RunJobs();
        Log(profileName, $"Entered {seedWords.Split(' ').Length} seed words into import field");

        // Click Submit Import
        await window.ClickButton("BtnSubmitImport", TestHelpers.UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Wait for success panel
        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet import");
        Log(profileName, "Import succeeded — SuccessPanel visible");

        await window.ClickButton("BtnCreateWalletDone", TestHelpers.UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Wallet imported successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Profile and infrastructure helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task WithProfileWindow(
        string profileName,
        HashSet<string> initializedProfiles,
        Func<Window, Task> action)
    {
        using var profileScope = TestProfileScope.For(profileName);
        var window = TestHelpers.CreateShellWindow();

        try
        {
            ValidateCurrentProfile(profileName);

            if (!initializedProfiles.Contains(profileName))
            {
                Log(profileName, "First use for profile. Wiping existing data...");
                await WipeExistingData(window, profileName);
                initializedProfiles.Add(profileName);
            }

            await action(window);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(250);
        }
    }

    private static void ValidateCurrentProfile(string expectedProfile)
    {
        var profileContext = global::App.App.Services.GetRequiredService<ProfileContext>();
        profileContext.ProfileName.Should().Be(expectedProfile);
    }

    private async Task WipeExistingData(Window window, string profileName)
    {
        await window.WipeExistingData();
        Log(profileName, "Profile data wiped.");
    }

    private async Task FundWalletViaFaucet(Window window, string profileName)
    {
        await window.FundWalletViaFaucet();
        Log(profileName, $"Wallet funded. Balance: {window.GetFundsViewModel()?.TotalBalance}");
    }

    private async Task OpenCreateWizard(Window window, MyProjectsViewModel myProjectsVm, string profileName)
    {
        await window.OpenCreateWizard(myProjectsVm);
        Log(profileName, "Create wizard opened.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Utility methods
    // ═══════════════════════════════════════════════════════════════════

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

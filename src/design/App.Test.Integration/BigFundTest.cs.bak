using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Utilities;
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
using System.Globalization;

namespace App.Test.Integration;

/// <summary>
/// Big multi-investor fund project test with 15 investors.
/// 
/// Setup:
/// - 1 founder creates a fund project with threshold = 0.01 BTC, penalty days = 0,
///   3 installment counts (3 and 6), weekly payout on today's day.
/// - Investors 1-7 invest below threshold (0.001 BTC) — auto-approved.
/// - Investors 8-15 invest above threshold (0.02 BTC) — require founder approval.
/// - Investors pick different installment patterns:
///   - Investors 1-5 and 8-12 select the 6-stage pattern.
///   - Investors 6-7 and 13-15 select the 3-stage pattern.
/// 
/// Flow:
/// 1. Founder creates the fund project.
/// 2. Each investor is processed one-by-one:
///    a. Investor invests.
///    b. If above-threshold: founder approves, then investor confirms.
/// 3. Founder claims stage 1 (should see UTXOs from all 15 investors).
/// 4. Above-threshold investors recover with penalty, then release from penalty.
/// 5. Below-threshold investors recover via end-of-project claim.
/// </summary>
public class BigFundTest
{
    private const string TestName = "BigFund";
    private const string FounderProfile = TestName + "-Founder";
    private const int TotalInvestors = 15;
    private const int BelowThresholdCount = 7;   // Investors 1-7
    private const int AboveThresholdCount = 8;    // Investors 8-15

    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(10);

    private sealed record ProjectHandle(string RunId, string ProjectName, string ProjectIdentifier, string FounderWalletId);

    private sealed record InvestorConfig(string ProfileName, string AmountBtc, bool ExpectFounderApproval, int TargetPatternStageCount);

    private static string InvestorProfile(int index) => $"{TestName}-Investor{index}";

    private static List<InvestorConfig> BuildInvestorConfigs()
    {
        var configs = new List<InvestorConfig>();

        // Below-threshold investors (1-7): auto-approved
        for (int i = 1; i <= BelowThresholdCount; i++)
        {
            // Investors 1-5 pick 6-stage pattern, 6-7 pick 3-stage pattern
            var stageCount = i <= 5 ? 6 : 3;
            configs.Add(new InvestorConfig(InvestorProfile(i), "0.001", false, stageCount));
        }

        // Above-threshold investors (8-15): require founder approval
        for (int i = BelowThresholdCount + 1; i <= TotalInvestors; i++)
        {
            // Investors 8-12 pick 6-stage pattern, 13-15 pick 3-stage pattern
            var stageCount = i <= 12 ? 6 : 3;
            configs.Add(new InvestorConfig(InvestorProfile(i), "0.02", true, stageCount));
        }

        return configs;
    }

    [AvaloniaFact]
    public async Task BigFund()
    {
        var initializedProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Big Fund {runId}";
        var projectAbout = $"{TestName} run {runId}. 15-investor fund project with mixed thresholds and installment patterns.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var thresholdAmountBtc = "0.01";
        var payoutDay = DateTime.UtcNow.DayOfWeek.ToString();
        var investors = BuildInvestorConfigs();

        Log(null, $"========== STARTING {nameof(BigFund)} ==========");
        Log(null, $"Run ID: {runId}");
        Log(null, $"Founder profile: {FounderProfile}");
        Log(null, $"Total investors: {TotalInvestors} ({BelowThresholdCount} below threshold, {AboveThresholdCount} above threshold)");
        foreach (var inv in investors)
        {
            Log(null, $"  {inv.ProfileName}: amount={inv.AmountBtc} BTC, approval={inv.ExpectFounderApproval}, pattern={inv.TargetPatternStageCount}-stage");
        }

        // ── Step 1: Founder creates the fund project ──
        ProjectHandle? project = null;

        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, FounderProfile);
            project = await CreateFundProjectAsync(
                window, FounderProfile, projectName, projectAbout,
                bannerImageUrl, profileImageUrl, thresholdAmountBtc, payoutDay, runId);
        });

        project.Should().NotBeNull();

        // ── Step 2: Process each investor one-by-one (invest → approve → confirm) ──
        var approvedSoFar = 0;
        for (var i = 0; i < investors.Count; i++)
        {
            var inv = investors[i];
            Log(null, $"────── Investor {i + 1}/{investors.Count}: {inv.ProfileName} ({inv.AmountBtc} BTC, approval={inv.ExpectFounderApproval}, pattern={inv.TargetPatternStageCount}-stage) ──────");

            // 2a. Investor invests
            await WithProfileWindow(inv.ProfileName, initializedProfiles, async window =>
            {
                await CreateWalletAndFundAsync(window, inv.ProfileName);
                await InvestInProjectAsync(
                    window, inv.ProfileName, project!, inv.AmountBtc,
                    inv.ExpectFounderApproval, inv.TargetPatternStageCount);
            });

            // 2b. If above-threshold, founder approves this single investor then investor confirms
            if (inv.ExpectFounderApproval)
            {
                approvedSoFar++;

                await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
                {
                    await ApproveSinglePendingInvestmentAsync(window, FounderProfile, project!, approvedSoFar);
                });

                await WithProfileWindow(inv.ProfileName, initializedProfiles, async window =>
                {
                    await ConfirmApprovedInvestmentAsync(window, inv.ProfileName, project!);
                });
            }
        }

        // ── Step 3: Founder claims stage 1 (UTXOs from all 15 investors) ──
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await ClaimStageOneAsync(window, FounderProfile, project!, TotalInvestors);
        });

        // ── Step 4: Above-threshold investors recover with penalty then penalty release ──
        foreach (var inv in investors.Where(i => i.ExpectFounderApproval))
        {
            await WithProfileWindow(inv.ProfileName, initializedProfiles, async window =>
            {
                await RecoverAboveThresholdInvestmentAsync(window, inv.ProfileName, project!);
            });
        }

        // ── Step 5: Below-threshold investors recover via end-of-project claim ──
        foreach (var inv in investors.Where(i => !i.ExpectFounderApproval))
        {
            await WithProfileWindow(inv.ProfileName, initializedProfiles, async window =>
            {
                await RecoverBelowThresholdInvestmentAsync(window, inv.ProfileName, project!);
            });
        }

        Log(null, $"========== {nameof(BigFund)} PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Profile Window Management
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

            SetPasswordProvider(profileName);
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
        var storage = global::App.App.Services.GetRequiredService<IApplicationStorage>();
        var profileDirectory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);

        profileContext.ProfileName.Should().Be(expectedProfile);
        Log(expectedProfile, $"Using profile directory: {profileDirectory}");
    }

    private static void SetPasswordProvider(string profileName)
    {
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log(profileName, "Set SimplePasswordProvider key to 'default-key'.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wallet Creation & Funding
    // ═══════════════════════════════════════════════════════════════════

    private async Task CreateWalletAndFundAsync(Window window, string profileName)
    {
        await window.NavigateToSectionAndVerify("Funds");

        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();

        if (!fundsVm!.SeedGroups.Any() || !fundsVm.SeedGroups.SelectMany(g => g.Wallets).Any())
        {
            Log(profileName, "Creating wallet via Generate flow...");
            await CreateWalletViaGenerate(window, profileName);
        }
        else
        {
            Log(profileName, "Wallet already exists for this profile.");
        }

        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull();

        Log(profileName, "Funding wallet via faucet...");
        await FundWalletViaFaucet(window, profileName);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fund Project Creation
    // ═══════════════════════════════════════════════════════════════════

    private async Task<ProjectHandle> CreateFundProjectAsync(
        Window window,
        string profileName,
        string projectName,
        string projectAbout,
        string bannerImageUrl,
        string profileImageUrl,
        string thresholdAmountBtc,
        string payoutDay,
        string runId)
    {
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!, profileName);

        var wizardVm = myProjectsVm.CreateProjectVm;
        wizardVm.Should().NotBeNull();

        Log(profileName, "Selecting Fund project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("fund");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, $"Setting project metadata: {projectName}");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        wizardVm.ProjectName.Should().Be(projectName);
        wizardVm.ProjectAbout.Should().Be(projectAbout);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Setting project images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.BannerUrl.Should().Be(bannerImageUrl);
        wizardVm.ProfileUrl.Should().Be(profileImageUrl);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Configuring target amount, threshold, and zero penalty days...");
        wizardVm.TargetAmount = "1.0";
        wizardVm.ApprovalThreshold = thresholdAmountBtc;
        wizardVm.PenaltyDays = 0;
        wizardVm.TargetAmount.Should().Be("1.0");
        wizardVm.ApprovalThreshold.Should().Be(thresholdAmountBtc);
        wizardVm.PenaltyDays.Should().Be(0);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, $"Configuring payout schedule for today ({payoutDay}) with 3 and 6 installments...");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.PayoutFrequency = "Weekly";
        wizardVm.ToggleInstallmentCount(3);
        wizardVm.ToggleInstallmentCount(6);
        wizardVm.WeeklyPayoutDay = payoutDay;
        wizardVm.GeneratePayoutSchedule();
        wizardVm.Stages.Count.Should().Be(6, "stages preview uses the max selected installment count");
        wizardVm.Stages.Select(s => s.StageNumber).Should().ContainInOrder(1, 2, 3, 4, 5, 6);
        wizardVm.SelectedInstallmentCounts.Should().HaveCount(2, "project should have two instalment patterns (3-month and 6-month)");
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

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

        var deployDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < deployDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (deployVm.CurrentScreen == DeployScreen.Success)
            {
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

        // Poll for the project to appear (indexer may lag after deploy)
        MyProjectItemViewModel? project = null;
        var projectPollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < projectPollDeadline)
        {
            await myProjectsVm.LoadFounderProjectsAsync();
            Dispatcher.UIThread.RunJobs();
            project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId, StringComparison.Ordinal));
            if (project != null)
            {
                break;
            }

            Log(profileName, "Project not found in MyProjects yet. Retrying...");
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        project.Should().NotBeNull();
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty();
        project.OwnerWalletId.Should().NotBeNullOrEmpty();
        project.Name.Should().Be(projectName);
        project.Description.Should().Contain(runId);
        project.ProjectType.Should().Be("fund");

        Log(profileName, $"Project deployed. ProjectId={project.ProjectIdentifier}, OwnerWalletId={project.OwnerWalletId}");
        return new ProjectHandle(runId, projectName, project.ProjectIdentifier!, project.OwnerWalletId!);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Investing
    // ═══════════════════════════════════════════════════════════════════

    private async Task InvestInProjectAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        string amountBtc,
        bool expectFounderApproval,
        int targetPatternStageCount)
    {
        var foundProject = await FindProjectFromSdkAsync(window, profileName, project);

        var findProjectsVm = GetFindProjectsViewModel(window);
        findProjectsVm.Should().NotBeNull();

        findProjectsVm!.OpenProjectDetail(foundProject);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);

        findProjectsVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var investVm = findProjectsVm.InvestPageViewModel;
        investVm.Should().NotBeNull();

        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline && investVm!.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }

        investVm!.Wallets.Count.Should().BeGreaterThan(0);

        // ── Select funding pattern via UI ──
        Log(profileName, $"Selecting funding pattern with {targetPatternStageCount} stages via UI...");

        investVm.FundingPatterns.Should().HaveCountGreaterThan(1,
            "project should expose multiple funding patterns");

        var investPageView = window.GetVisualDescendants()
            .OfType<InvestPageView>()
            .FirstOrDefault();
        investPageView.Should().NotBeNull("InvestPageView should be in the visual tree");

        var patternBorders = investPageView!.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Name == "FundPatternBorder")
            .ToList();
        patternBorders.Should().HaveCountGreaterThan(1,
            "invest page should render multiple FundPatternBorder elements");

        var targetBorder = patternBorders.FirstOrDefault(b =>
            b.DataContext is FundingPatternOption opt && opt.StageCount == targetPatternStageCount);
        targetBorder.Should().NotBeNull(
            $"a FundPatternBorder with StageCount={targetPatternStageCount} should exist");

        var targetOption = (FundingPatternOption)targetBorder!.DataContext!;
        investVm.SelectFundingPattern(targetOption);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        investVm.SelectedFundingPattern.Should().NotBeNull();
        investVm.SelectedFundingPattern!.StageCount.Should().Be(targetPatternStageCount,
            $"selected funding pattern should have {targetPatternStageCount} stages after selection");
        Log(profileName, $"Funding pattern with {targetPatternStageCount} stages selected. PatternId={investVm.SelectedFundingPattern.PatternId}");

        investVm.InvestmentAmount = amountBtc;
        investVm.CanSubmit.Should().BeTrue();
        investVm.Stages.Count.Should().BeGreaterThanOrEqualTo(targetPatternStageCount,
            $"fund project should expose at least {targetPatternStageCount} stage outputs for the selected pattern");
        investVm.Stages.Select(s => s.LabelText).Should().Contain(label => label.Contains("Stage 1"));
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var investWallet = investVm.Wallets[0];
        investVm.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, $"Investing {amountBtc} BTC with wallet {investWallet.Id.Value}...");

        const int maxPayAttempts = 3;
        for (var payAttempt = 1; payAttempt <= maxPayAttempts; payAttempt++)
        {
            investVm.PayWithWallet();

            var investDeadline = DateTime.UtcNow + TransactionTimeout;
            while (DateTime.UtcNow < investDeadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (investVm.CurrentScreen == InvestScreen.Success)
                {
                    break;
                }

                // Detect build/publish error while still on WalletSelector
                if (investVm.CurrentScreen == InvestScreen.WalletSelector && investVm.HasError)
                {
                    break;
                }

                await Task.Delay(PollInterval);
            }

            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                break;
            }

            if (payAttempt < maxPayAttempts && investVm.CurrentScreen == InvestScreen.WalletSelector && investVm.HasError)
            {
                Log(profileName, $"PayWithWallet attempt #{payAttempt} failed: {investVm.ErrorMessage}. Retrying after delay...");
                await Task.Delay(TimeSpan.FromSeconds(5));
                Dispatcher.UIThread.RunJobs();
                continue;
            }
        }

        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"Invest should reach success. Last status: {investVm.PaymentStatusText}, Error: {investVm.ErrorMessage}");
        investVm.FormattedAmount.Should().Be(decimal.Parse(amountBtc, CultureInfo.InvariantCulture).ToString("F8", CultureInfo.InvariantCulture));

        // Verify auto-approval status
        Log(profileName, $"IsAutoApproved={investVm.IsAutoApproved}, SuccessTitle='{investVm.SuccessTitle}'");
        if (expectFounderApproval)
        {
            investVm.IsAutoApproved.Should().BeFalse("above-threshold investment should NOT be auto-approved");
            investVm.SuccessTitle.Should().Contain("Pending Approval",
                "above-threshold investment should show 'Pending Approval' in SuccessTitle");
        }
        else
        {
            investVm.IsAutoApproved.Should().BeTrue("below-threshold investment should be auto-approved");
            investVm.SuccessTitle.Should().Contain("Successful",
                "below-threshold (auto-approved) investment should show 'Successful' in SuccessTitle");
        }

        // DIRECT DI RESOLVE: PortfolioViewModel is a singleton not reachable from the visual
        // tree while we're still on the Find Projects invest flow.
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        var addedInvestment = portfolioVm.Investments.FirstOrDefault(i => i.ProjectIdentifier == project.ProjectIdentifier);
        addedInvestment.Should().NotBeNull();
        addedInvestment.ProjectName.Should().Be(project.ProjectName);
        var actualInvested = decimal.Parse(addedInvestment.TotalInvested, CultureInfo.InvariantCulture);
        var expectedInvested = decimal.Parse(amountBtc, CultureInfo.InvariantCulture);
        actualInvested.Should().BeGreaterThan(0, "TotalInvested should be non-zero after AddToPortfolio");
        actualInvested.Should().BeLessThanOrEqualTo(expectedInvested, "TotalInvested should not exceed requested amount");
        (expectedInvested - actualInvested).Should().BeLessThan(0.001m, "TotalInvested should be within fee tolerance of requested amount");
        Log(profileName, $"Investment completed. Founder approval expected: {expectFounderApproval}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Founder Approval (single investor at a time)
    // ═══════════════════════════════════════════════════════════════════

    private async Task ApproveSinglePendingInvestmentAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        int expectedTotalAboveThresholdSoFar)
    {
        await window.NavigateToSectionAndVerify("Funders");

        var fundersVm = GetFundersViewModel(window);
        fundersVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        SignatureRequestViewModel? pendingSignature = null;

        while (DateTime.UtcNow < deadline)
        {
            await fundersVm!.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("waiting");
            Dispatcher.UIThread.RunJobs();

            var waitingForProject = fundersVm.FilteredSignatures
                .Where(s => string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal))
                .ToList();

            Log(profileName,
                $"Funders waiting count for project: {waitingForProject.Count} (expecting at least 1 unapproved)");

            if (waitingForProject.Count > 0)
            {
                // Take the first waiting one (any order is fine — they're all unapproved)
                pendingSignature = waitingForProject.First();
                break;
            }

            await Task.Delay(PollInterval);
        }

        pendingSignature.Should().NotBeNull("there should be a pending signature request to approve");

        Log(profileName,
            $"Approving signature request id={pendingSignature!.Id} for project {project.ProjectIdentifier}");
        await window.ClickApproveSignatureAsync(fundersVm!, pendingSignature, UiTimeout);

        // Wait for approval to register
        var approvalDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < approvalDeadline)
        {
            await fundersVm!.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("approved");
            Dispatcher.UIThread.RunJobs();

            var approvedCount = fundersVm.FilteredSignatures.Count(s =>
                string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
            if (approvedCount >= expectedTotalAboveThresholdSoFar)
            {
                Log(profileName, $"Approved count now {approvedCount} (expected {expectedTotalAboveThresholdSoFar}).");
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Founder approval did not complete in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Founder Approval (batch - kept for reference)
    // ═══════════════════════════════════════════════════════════════════

    private async Task ApprovePendingInvestmentsAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        int expectedPendingCount)
    {
        await window.NavigateToSectionAndVerify("Funders");

        var fundersVm = GetFundersViewModel(window);
        fundersVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        List<SignatureRequestViewModel>? pendingSignatures = null;

        while (DateTime.UtcNow < deadline)
        {
            await fundersVm!.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("waiting");
            Dispatcher.UIThread.RunJobs();

            pendingSignatures = fundersVm.FilteredSignatures
                .Where(s => string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal))
                .OrderBy(s => s.AmountSats)
                .ToList();

            Log(profileName,
                $"Funders waiting count: {fundersVm.WaitingCount}, project waiting count: {pendingSignatures.Count}/{expectedPendingCount}");

            if (pendingSignatures.Count == expectedPendingCount)
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        pendingSignatures.Should().NotBeNull();
        pendingSignatures!.Count.Should().Be(expectedPendingCount,
            $"all {expectedPendingCount} above-threshold investment requests should appear in Funders before approval");

        foreach (var pendingSignature in pendingSignatures)
        {
            Log(profileName,
                $"Approving signature request id={pendingSignature.Id} for project {project.ProjectIdentifier}");
            await window.ClickApproveSignatureAsync(fundersVm!, pendingSignature, UiTimeout);
        }

        var approvalDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < approvalDeadline)
        {
            await fundersVm!.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("approved");
            Dispatcher.UIThread.RunJobs();

            var approvedCount = fundersVm.FilteredSignatures.Count(s =>
                string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
            if (approvedCount >= expectedPendingCount)
            {
                Log(profileName, $"Founder approved {approvedCount} investment requests.");
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Founder approvals did not complete in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Investor Confirmation
    // ═══════════════════════════════════════════════════════════════════

    private async Task ConfirmApprovedInvestmentAsync(Window window, string profileName, ProjectHandle project)
    {
        var portfolioVm = await WaitForPortfolioInvestmentAsync(window, profileName, project, investment => investment.Step >= 2);
        var investment = portfolioVm.Investments.First(i => i.ProjectIdentifier == project.ProjectIdentifier);

        Log(profileName, $"Confirming approved investment. Step={investment.Step}, Status={investment.StatusText}");
        investment.ApprovalStatus.Should().Be("Approved");
        await window.ClickInvestmentDetailActionAsync(portfolioVm, investment, "ConfirmInvestmentButton", UiTimeout);

        var activeDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < activeDeadline)
        {
            await portfolioVm.LoadInvestmentsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            var refreshed = portfolioVm.Investments.FirstOrDefault(i => i.ProjectIdentifier == project.ProjectIdentifier);
            if (refreshed?.Step == 3)
            {
                Log(profileName, "Investor confirmation completed and investment is active.");
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Confirmed investment did not become active in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Founder Claims Stage 1
    // ═══════════════════════════════════════════════════════════════════

    private async Task ClaimStageOneAsync(Window window, string profileName, ProjectHandle project, int expectedUtxoCount)
    {
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await myProjectsVm!.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var founderProject = myProjectsVm.Projects.FirstOrDefault(p =>
            string.Equals(p.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
        founderProject.Should().NotBeNull();

        var manageVm = myProjectsVm.SelectedManageProject;

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await myProjectsVm!.LoadFounderProjectsAsync();
            myProjectsVm.OpenManageProject(founderProject!);
            manageVm = myProjectsVm.SelectedManageProject;
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var stageSnapshot = string.Join(", ", manageVm.Stages.Select(s =>
                $"#{s.Number}:available={s.Available},canClaim={s.CanClaim},availableTxs={s.AvailableTransactions.Count},spent={s.SpentTransactionCount},date='{s.CompletionDate}'"));
            Log(profileName, $"Stage snapshot: {stageSnapshot}");

            var stage1 = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.AvailableTransactions.Count >= expectedUtxoCount);
            if (stage1 != null)
            {
                stage1.AvailableTransactions.Count.Should().Be(expectedUtxoCount,
                    $"founder should claim stage 1 from all {expectedUtxoCount} investor UTXOs");

                await window.ClickManageProjectClaimStageAsync(myProjectsVm, founderProject!, stage1.Number, UiTimeout);
                Log(profileName, $"Founder claimed stage 1 using {expectedUtxoCount} available UTXOs.");
                break;
            }

            await Task.Delay(PollInterval);
        }

        var spentDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < spentDeadline)
        {
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            if (manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0))
            {
                Log(profileName, "Founder spend is visible in stage status.");
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Founder stage 1 spend was not indexed in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Recovery: Above Threshold (penalty -> penalty release)
    // ═══════════════════════════════════════════════════════════════════

    private async Task RecoverAboveThresholdInvestmentAsync(Window window, string profileName, ProjectHandle project)
    {
        var portfolioVm = await WaitForPortfolioInvestmentAsync(window, profileName, project, investment => investment.Step == 3);
        var investment = portfolioVm.Investments.First(i => i.ProjectIdentifier == project.ProjectIdentifier);
        investment.InvestmentWalletId.Should().NotBeNullOrEmpty();
        investment.InvestmentTransactionId.Should().NotBeNullOrEmpty();

        var recoveryState = await WaitForRecoveryActionAsync(portfolioVm, investment, profileName, expectedActionKey: "recovery");
        recoveryState.ActionKey.Should().Be("recovery");

        Log(profileName, "Recovering above-threshold investment to penalty...");
        await EnsureWalletHasFeeFunds(window, profileName, investment.InvestmentWalletId, "before recover-to-penalty");
        var recoverToPenaltyResult = await ExecuteRecoveryActionWithRetry(
            profileName,
            "recover-to-penalty",
            () => portfolioVm.RecoverFundsAsync(investment).ContinueWith(t => t.Result.Success),
            () => LogRecoveryBuildDiagnostics(investment, RecoveryAction.Recovery));
        recoverToPenaltyResult.Should().BeTrue();

        var postPenaltyState = await WaitForRecoveryActionAsync(portfolioVm, investment, profileName, expectedActionKey: "penaltyRelease");
        postPenaltyState.ActionKey.Should().Be("penaltyRelease");

        Log(profileName, "Recovering from penalty (penalty days = 0)...");
        await EnsureWalletHasFeeFunds(window, profileName, investment.InvestmentWalletId, "before penalty release");
        var penaltyReleaseResult = await ExecuteRecoveryActionWithRetry(
            profileName,
            "penalty-release",
            () => portfolioVm.PenaltyReleaseFundsAsync(investment).ContinueWith(t => t.Result.Success),
            () => LogRecoveryBuildDiagnostics(investment, RecoveryAction.PenaltyRelease));
        penaltyReleaseResult.Should().BeTrue();

        Log(profileName, "Verifying stage statuses after penalty release...");
        var statusDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < statusDeadline)
        {
            await portfolioVm.LoadRecoveryStatusAsync(investment);
            Dispatcher.UIThread.RunJobs();

            var recoveredStages = investment.Stages
                .Where(s => s.Status == "Recovered after penalty" || s.Status == "Spent by investor")
                .ToList();

            if (recoveredStages.Count > 0)
            {
                Log(profileName, $"Found {recoveredStages.Count} stage(s) with recovered status: {string.Join(", ", recoveredStages.Select(s => $"stage {s.StageNumber}='{s.Status}'"))}");
                foreach (var stage in recoveredStages)
                {
                    stage.IsStatusRecovered.Should().BeTrue($"stage {stage.StageNumber} with status '{stage.Status}' should be recognized as recovered");
                }
                break;
            }

            await Task.Delay(PollInterval);
        }

        investment.Stages.Any(s => s.Status == "Recovered after penalty" || s.Status == "Spent by investor").Should().BeTrue(
            "at least one stage should show 'Recovered after penalty' or 'Spent by investor' after penalty release");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Recovery: Below Threshold (end-of-project claim)
    // ═══════════════════════════════════════════════════════════════════

    private async Task RecoverBelowThresholdInvestmentAsync(Window window, string profileName, ProjectHandle project)
    {
        var portfolioVm = await WaitForPortfolioInvestmentAsync(window, profileName, project, investment => investment.Step == 3);
        var investment = portfolioVm.Investments.First(i => i.ProjectIdentifier == project.ProjectIdentifier);
        investment.InvestmentWalletId.Should().NotBeNullOrEmpty();
        investment.InvestmentTransactionId.Should().NotBeNullOrEmpty();

        var recoveryState = await WaitForRecoveryActionAsync(portfolioVm, investment, profileName, expectedActionKey: "belowThreshold");
        recoveryState.ActionKey.Should().Be("belowThreshold");

        Log(profileName, "Recovering below-threshold investment without penalty...");
        await EnsureWalletHasFeeFunds(window, profileName, investment.InvestmentWalletId, "before end-of-project claim");
        var recoverResult = await ExecuteRecoveryActionWithRetry(
            profileName,
            "end-of-project-claim",
            () => portfolioVm.ClaimEndOfProjectAsync(investment).ContinueWith(t => t.Result.Success),
            () => LogRecoveryBuildDiagnostics(investment, RecoveryAction.EndOfProject));
        recoverResult.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Retry & Diagnostics
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> ExecuteRecoveryActionWithRetry(
        string profileName,
        string actionLabel,
        Func<Task<bool>> execute,
        Func<Task> logDiagnostics)
    {
        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            Log(profileName, $"Attempt #{attempt} for recovery action '{actionLabel}'...");
            var result = await execute();
            if (result)
            {
                Log(profileName, $"Recovery action '{actionLabel}' succeeded on attempt #{attempt}.");
                return true;
            }

            await logDiagnostics();
            await Task.Delay(PollInterval);
        }

        return false;
    }

    /// DIAGNOSTIC SDK CALL: Logs the raw Build*Transaction result to help diagnose
    /// why the UI-level recovery/release retry may be failing. Not part of the test flow.
    private static async Task LogRecoveryBuildDiagnostics(InvestmentViewModel investment, RecoveryAction action)
    {
        var investmentAppService = global::App.App.Services.GetRequiredService<IInvestmentAppService>();
        var walletId = new WalletId(investment.InvestmentWalletId);
        var projectId = new ProjectId(investment.ProjectIdentifier);
        var feeRate = new DomainFeerate(20);

        switch (action)
        {
            case RecoveryAction.Recovery:
            {
                var buildResult = await investmentAppService.BuildRecoveryTransaction(
                    new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(walletId, projectId, feeRate));
                Log(investment.ProjectIdentifier, buildResult.IsSuccess
                    ? $"Diagnostic BuildRecoveryTransaction succeeded. TxId={buildResult.Value.TransactionDraft.TransactionId}"
                    : $"Diagnostic BuildRecoveryTransaction failed: {buildResult.Error}");
                break;
            }
            case RecoveryAction.PenaltyRelease:
            {
                var buildResult = await investmentAppService.BuildPenaltyReleaseTransaction(
                    new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(walletId, projectId, feeRate));
                Log(investment.ProjectIdentifier, buildResult.IsSuccess
                    ? $"Diagnostic BuildPenaltyReleaseTransaction succeeded. TxId={buildResult.Value.TransactionDraft.TransactionId}"
                    : $"Diagnostic BuildPenaltyReleaseTransaction failed: {buildResult.Error}");
                break;
            }
            case RecoveryAction.EndOfProject:
            {
                var buildResult = await investmentAppService.BuildEndOfProjectClaim(
                    new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(walletId, projectId, feeRate));
                Log(investment.ProjectIdentifier, buildResult.IsSuccess
                    ? $"Diagnostic BuildEndOfProjectClaim succeeded. TxId={buildResult.Value.TransactionDraft.TransactionId}"
                    : $"Diagnostic BuildEndOfProjectClaim failed: {buildResult.Error}");
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wait Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<RecoveryState> WaitForRecoveryActionAsync(
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        string profileName,
        string expectedActionKey)
    {
        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await portfolioVm.LoadRecoveryStatusAsync(investment);
            Dispatcher.UIThread.RunJobs();

            Log(profileName,
                $"Recovery state: HasUnspent={investment.RecoveryState.HasUnspentItems}, InPenalty={investment.RecoveryState.HasSpendableItemsInPenalty}, HasReleaseSig={investment.RecoveryState.HasReleaseSignatures}, EndOfProject={investment.RecoveryState.EndOfProject}, AboveThreshold={investment.RecoveryState.IsAboveThreshold}, ActionKey={investment.RecoveryState.ActionKey}");

            if (investment.RecoveryState.ActionKey == expectedActionKey)
            {
                return investment.RecoveryState;
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException($"Recovery action '{expectedActionKey}' did not appear in time.");
    }

    private async Task<PortfolioViewModel> WaitForPortfolioInvestmentAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        Func<InvestmentViewModel, bool> predicate)
    {
        await window.NavigateToSectionAndVerify("Funded");

        // DIRECT DI RESOLVE: Need the singleton PortfolioViewModel to poll SDK reload.
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        var deadline = DateTime.UtcNow + IndexerLagTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await portfolioVm.LoadInvestmentsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            var investment = portfolioVm.Investments.FirstOrDefault(i =>
                string.Equals(i.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));

            if (investment != null)
            {
                Log(profileName, $"Portfolio investment found. Step={investment.Step}, Status={investment.StatusText}, Approval={investment.ApprovalStatus}, WalletId={investment.InvestmentWalletId}, InvestmentTxId={investment.InvestmentTransactionId}");
                if (predicate(investment))
                {
                    return portfolioVm;
                }
            }
            else
            {
                Log(profileName, "Portfolio investment not found yet.");
            }

            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Portfolio investment did not reach the expected state in time.");
    }

    private async Task EnsureWalletHasFeeFunds(Window window, string profileName, string walletId, string context)
    {
        await window.NavigateToSectionAndVerify("Funds");

        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            // DIRECT SDK CALL: FundsViewModel.TotalBalance doesn't expose the raw sats breakdown
            // (confirmed + unconfirmed + reserved) needed to check fee-level funding.
            var refresh = await global::App.App.Services.GetRequiredService<Angor.Sdk.Wallet.Application.IWalletAppService>()
                .RefreshAndGetAccountBalanceInfo(new WalletId(walletId));

            if (refresh.IsSuccess)
            {
                var info = refresh.Value;
                var available = info.TotalBalance + info.TotalUnconfirmedBalance + info.TotalBalanceReserved;
                Log(profileName, $"Wallet balance {context}: {available.ToUnitBtc():F8} BTC available for fees");
                if (available > 20_000)
                {
                    return;
                }
            }

            Log(profileName, $"Wallet needs fee funds {context}. Requesting faucet coins...");
            var (success, error) = await fundsVm!.GetTestCoinsAsync(walletId);
            Log(profileName, success ? "Fee-funding faucet request accepted." : $"Fee-funding faucet request failed: {error}");

            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        throw new InvalidOperationException($"Wallet '{walletId}' did not receive enough fee funds {context}.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Project Discovery
    // ═══════════════════════════════════════════════════════════════════

    private async Task<ProjectItemViewModel> FindProjectFromSdkAsync(Window window, string profileName, ProjectHandle project)
    {
        await window.NavigateToSectionAndVerify("Find Projects");

        var findProjectsVm = GetFindProjectsViewModel(window);
        findProjectsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await findProjectsVm!.LoadAllProjectsFromSdkAsync();

            var foundProject = findProjectsVm.Projects.FirstOrDefault(p =>
                string.Equals(p.ProjectId, project.ProjectIdentifier, StringComparison.Ordinal) ||
                p.Description.Contains(project.RunId, StringComparison.Ordinal) ||
                p.ShortDescription.Contains(project.RunId, StringComparison.Ordinal));

            if (foundProject != null)
            {
                Log(profileName, $"Found project in SDK list: {foundProject.ProjectId}");
                return foundProject;
            }

            Log(profileName, "Project not found in SDK yet. Retrying...");
            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Project was not found in the SDK project list in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Common UI Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task WipeExistingData(Window window, string profileName)
    {
        await window.NavigateToSettingsAndVerify();

        var settingsView = window.GetVisualDescendants().OfType<SettingsView>().FirstOrDefault();
        if (settingsView?.DataContext is SettingsViewModel settingsVm)
        {
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
            Log(profileName, "Profile data wiped.");
        }
    }

    private async Task CreateWalletViaGenerate(Window window, string profileName)
    {
        var addWalletBtn = FindAddWalletButton(window);
        addWalletBtn.Should().NotBeNull();

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
        successPanel.Should().NotBeNull();
        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Wallet created successfully.");
    }

    private async Task FundWalletViaFaucet(Window window, string profileName)
    {
        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();

        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty();

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;
        var faucetAttempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (fundsVm.TotalBalance != "0.0000")
            {
                Log(profileName, $"Non-zero balance detected: {fundsVm.TotalBalance}");
                return;
            }

            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                faucetAttempts++;
                lastFaucetAttempt = DateTime.UtcNow;
                Log(profileName, $"Faucet attempt #{faucetAttempts}...");

                (bool success, string? error) = await fundsVm.GetTestCoinsAsync(walletId!);
                Dispatcher.UIThread.RunJobs();
                Log(profileName, success ? "Faucet request accepted." : $"Faucet request failed: {error}");
            }

            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        throw new InvalidOperationException("Wallet balance did not become non-zero in time.");
    }

    private async Task OpenCreateWizard(Window window, MyProjectsViewModel myProjectsVm, string profileName)
    {
        myProjectsVm.CreateProjectVm.ResetWizard();
        myProjectsVm.LaunchCreateWizard();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.ShowCreateWizard.Should().BeTrue();
        myProjectsVm.CreateProjectVm.OnProjectDeployed = () =>
        {
            myProjectsVm.OnProjectDeployed(myProjectsVm.CreateProjectVm);
            myProjectsVm.CloseCreateWizard();
        };

        Log(profileName, "Create wizard opened.");
    }

    private static Button? FindAddWalletButton(Window window)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible);
        foreach (var btn in buttons)
        {
            if (btn.Content is string text && text.Contains("Add Wallet", StringComparison.Ordinal))
            {
                return btn;
            }

            if (btn.Content is StackPanel panel)
            {
                foreach (var child in panel.Children.OfType<TextBlock>())
                {
                    if (child.Text == "Add Wallet")
                    {
                        return btn;
                    }
                }
            }
        }

        return null;
    }

    private async Task ClickWalletCardButton(Window window, string automationId)
    {
        var button = await window.WaitForControl<Button>(automationId, UiTimeout);
        button.Should().NotBeNull();
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ViewModel Getters
    // ═══════════════════════════════════════════════════════════════════

    private static FundsViewModel? GetFundsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FundsView>().FirstOrDefault()?.DataContext as FundsViewModel;
    }

    private static MyProjectsViewModel? GetMyProjectsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<MyProjectsView>().FirstOrDefault()?.DataContext as MyProjectsViewModel;
    }

    private static FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FindProjectsView>().FirstOrDefault()?.DataContext as FindProjectsViewModel;
    }

    private static FundersViewModel? GetFundersViewModel(Window window)
    {
        return window.GetVisualDescendants().OfType<FundersView>().FirstOrDefault()?.DataContext as FundersViewModel;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Logging & Enums
    // ═══════════════════════════════════════════════════════════════════

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }

    private enum RecoveryAction
    {
        Recovery,
        PenaltyRelease,
        EndOfProject
    }
}

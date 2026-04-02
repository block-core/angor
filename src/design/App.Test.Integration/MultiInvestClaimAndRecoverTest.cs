using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
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

public class MultiInvestClaimAndRecoverTest
{
    private const string TestName = "MultiInvestClaimAndRecover";
    private const string FounderProfile = TestName + "-Founder";
    private const string Investor1Profile = TestName + "-Investor1";
    private const string Investor2Profile = TestName + "-Investor2";

    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);

    private sealed record ProjectHandle(string RunId, string ProjectName, string ProjectIdentifier, string FounderWalletId);

    [AvaloniaFact]
    public async Task MultiInvestClaimAndRecover()
    {
        var initializedProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Multi Invest {runId}";
        var projectAbout =
            $"{TestName} run {runId}. Founder claims stage 1, then investors claim the remaining stages.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var investor1AmountBtc = "0.02";
        var investor2AmountBtc = "0.03";

        Log(null, $"========== STARTING {nameof(MultiInvestClaimAndRecover)} ==========");
        Log(null, $"Run ID: {runId}");
        Log(null, $"Founder profile: {FounderProfile}");
        Log(null, $"Investor1 profile: {Investor1Profile}");
        Log(null, $"Investor2 profile: {Investor2Profile}");

        ProjectHandle? project = null;

        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, FounderProfile);
            project = await CreateInvestProjectAsync(
                window,
                FounderProfile,
                projectName,
                projectAbout,
                bannerImageUrl,
                profileImageUrl,
                runId);
        });

        project.Should().NotBeNull();

        await WithProfileWindow(Investor1Profile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, Investor1Profile);
            await InvestInProjectAsync(window, Investor1Profile, project!, investor1AmountBtc);
        });

        await WithProfileWindow(Investor2Profile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, Investor2Profile);
            await InvestInProjectAsync(window, Investor2Profile, project!, investor2AmountBtc);
        });

        // Step 1: Founder approves the pending signature requests from both investors.
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await ApprovePendingInvestmentsAsync(window, FounderProfile, project!, expectedPendingCount: 2);
        });

        // Step 2: Both investors confirm (publish) their approved investment transactions on-chain.
        // This must happen BEFORE the founder can claim stages, because the indexer won't see any
        // investment UTXOs until the transactions are actually broadcast and indexed.
        await WithProfileWindow(Investor1Profile, initializedProfiles, async window =>
        {
            await ConfirmApprovedInvestmentAsync(window, Investor1Profile, project!);
        });

        await WithProfileWindow(Investor2Profile, initializedProfiles, async window =>
        {
            await ConfirmApprovedInvestmentAsync(window, Investor2Profile, project!);
        });

        // Step 3: Now that investment transactions are on-chain, founder can claim stage 1
        // and release remaining stages back to investors.
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await ClaimStageOneAsync(window, FounderProfile, project!);
            await ReleaseRemainingStagesToInvestorsAsync(window, FounderProfile, project!);
        });

        // Step 4: Each investor claims their remaining (released) stages.
        await WithProfileWindow(Investor1Profile, initializedProfiles, async window =>
        {
            await ClaimRemainingStagesAsync(window, Investor1Profile, project!);
        });

        await WithProfileWindow(Investor2Profile, initializedProfiles, async window =>
        {
            await ClaimRemainingStagesAsync(window, Investor2Profile, project!);
        });

        Log(null, $"========== {nameof(MultiInvestClaimAndRecover)} PASSED ==========");
    }

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

    private async Task CreateWalletAndFundAsync(Window window, string profileName)
    {
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

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

    private async Task<ProjectHandle> CreateInvestProjectAsync(
        Window window,
        string profileName,
        string projectName,
        string projectAbout,
        string bannerImageUrl,
        string profileImageUrl,
        string runId)
    {
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!, profileName);

        var wizardVm = myProjectsVm.CreateProjectVm;
        wizardVm.Should().NotBeNull();

        Log(profileName, "Selecting investment project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("investment");
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

        Log(profileName, "Configuring target amount and investment end date...");
        wizardVm.TargetAmount = "1.0";
        wizardVm.InvestEndDate = DateTime.UtcNow.AddMonths(3);
        wizardVm.TargetAmount.Should().Be("1.0");
        wizardVm.InvestEndDate.Should().NotBeNull();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Generating three monthly investment stages...");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.DurationValue = "3";
        wizardVm.DurationUnit = "Months";
        wizardVm.ReleaseFrequency = "Monthly";
        // Set start date ~120 days in the past so all 3 monthly stages (baseDate+30/+60/+90)
        // have release dates in the past, making them claimable (Unspent) instead of Locked.
        wizardVm.StartDate = DateTime.UtcNow.AddDays(-120).ToString("yyyy-MM-dd");
        wizardVm.GenerateInvestmentStages();
        wizardVm.Stages.Count.Should().Be(3);
        wizardVm.Stages.Select(s => s.StageNumber).Should().ContainInOrder(1, 2, 3);
        var totalPercentage = wizardVm.Stages
            .Select(s => s.Percentage.Replace("%", ""))
            .Select(p => double.Parse(p, CultureInfo.InvariantCulture))
            .Sum();
        totalPercentage.Should().BeApproximately(100.0, 1.0);
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

        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId, StringComparison.Ordinal));
        project.Should().NotBeNull();
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty();
        project.OwnerWalletId.Should().NotBeNullOrEmpty();
        project.Name.Should().Be(projectName);
        project.Description.Should().Contain(runId);
        project.ProjectType.Should().Be("investment");

        Log(profileName, $"Project deployed. ProjectId={project.ProjectIdentifier}, OwnerWalletId={project.OwnerWalletId}");
        return new ProjectHandle(runId, projectName, project.ProjectIdentifier!, project.OwnerWalletId!);
    }

    private async Task InvestInProjectAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        string amountBtc)
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
        investVm.InvestmentAmount = amountBtc;
        investVm.CanSubmit.Should().BeTrue();
        investVm.Stages.Count.Should().Be(3, "investment project should expose the generated fixed stages");
        investVm.Stages.Select(s => s.LabelText).Should().Contain(label => label.Contains("Stage 1"));
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var investWallet = investVm.Wallets[0];
        investVm.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, $"Investing {amountBtc} BTC with wallet {investWallet.Id.Value}...");
        investVm.PayWithWallet();

        var investDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < investDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"Invest should reach success. Last status: {investVm.PaymentStatusText}");
        investVm.FormattedAmount.Should().Be(
            decimal.Parse(amountBtc, CultureInfo.InvariantCulture).ToString("F8", CultureInfo.InvariantCulture));

        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        var addedInvestment = portfolioVm.Investments.FirstOrDefault(i => i.ProjectIdentifier == project.ProjectIdentifier);
        addedInvestment.Should().NotBeNull();
        addedInvestment!.ProjectName.Should().Be(project.ProjectName);
        addedInvestment.TotalInvested.Should().Be(
            decimal.Parse(amountBtc, CultureInfo.InvariantCulture).ToString("F8", CultureInfo.InvariantCulture));
        addedInvestment.ProjectType.Should().Be("invest");
        addedInvestment.Step.Should().Be(1, "above-threshold investments should wait for founder approval");
        addedInvestment.ApprovalStatus.Should().Be("Pending");
        addedInvestment.StatusText.Should().Be("Awaiting Approval");
        Log(profileName, "Investment request submitted and pending founder approval.");
    }

    private async Task ApprovePendingInvestmentsAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        int expectedPendingCount)
    {
        window.NavigateToSection("Funders");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

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
                $"Funders waiting count: {fundersVm.WaitingCount}, project waiting count: {pendingSignatures.Count}");

            if (pendingSignatures.Count == expectedPendingCount)
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        pendingSignatures.Should().NotBeNull();
        pendingSignatures!.Count.Should().Be(expectedPendingCount,
            "both investor requests should appear in Funders before approval");

        foreach (var pendingSignature in pendingSignatures)
        {
            Log(profileName,
                $"Approving signature request id={pendingSignature.Id} for project {project.ProjectIdentifier}");
            fundersVm!.ApproveSignature(pendingSignature.Id);
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

    private async Task ConfirmApprovedInvestmentAsync(Window window, string profileName, ProjectHandle project)
    {
        var portfolioVm = await WaitForPortfolioInvestmentAsync(
            window,
            profileName,
            project,
            investment => investment.Step >= 2);
        var investment = portfolioVm.Investments.First(i => i.ProjectIdentifier == project.ProjectIdentifier);

        Log(profileName, $"Confirming approved investment. Step={investment.Step}, Status={investment.StatusText}");
        investment.ApprovalStatus.Should().Be("Approved");
        investment.Step.Should().Be(2, "approved investments should require explicit investor confirmation");

        var confirmResult = await portfolioVm.ConfirmInvestmentAsync(investment);
        confirmResult.Should().BeTrue("founder-approved investment should be confirmable by the investor");

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

    private async Task ClaimStageOneAsync(Window window, string profileName, ProjectHandle project)
    {
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await myProjectsVm!.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var founderProject = myProjectsVm.Projects.FirstOrDefault(p =>
            string.Equals(p.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
        founderProject.Should().NotBeNull();

        myProjectsVm.OpenManageProject(founderProject!);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var manageVm = myProjectsVm.SelectedManageProject;
        manageVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var stageSnapshot = string.Join(", ", manageVm.Stages.Select(s =>
                $"#{s.Number}:available={s.Available},canClaim={s.CanClaim},availableTxs={s.AvailableTransactions.Count},spent={s.SpentTransactionCount}"));
            Log(profileName, $"Stage snapshot: {stageSnapshot}");

            var stage1 = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.AvailableTransactions.Count >= 2);
            if (stage1 != null)
            {
                stage1.AvailableTransactions.Count.Should().Be(2,
                    "founder should claim stage 1 from both investor UTXOs");

                var claimResult = await manageVm.ClaimStageFundsAsync(stage1.Number, stage1.AvailableTransactions.ToList());
                claimResult.Should().BeTrue();
                Log(profileName, "Founder claimed stage 1 using both available UTXOs.");
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

    private async Task ReleaseRemainingStagesToInvestorsAsync(Window window, string profileName, ProjectHandle project)
    {
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var myProjectsVm = GetMyProjectsViewModel(window);
        myProjectsVm.Should().NotBeNull();

        await myProjectsVm!.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var founderProject = myProjectsVm.Projects.FirstOrDefault(p =>
            string.Equals(p.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
        founderProject.Should().NotBeNull();

        myProjectsVm.OpenManageProject(founderProject!);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var manageVm = myProjectsVm.SelectedManageProject;
        manageVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var releaseResult = await manageVm!.ReleaseFundsToInvestorsAsync();
            if (releaseResult)
            {
                Log(profileName, "Founder released remaining stages to investors.");
                return;
            }

            Log(profileName, "Remaining stage release not ready yet. Retrying...");
            await Task.Delay(PollInterval);
        }

        throw new InvalidOperationException("Founder release signatures were not created in time.");
    }

    private async Task ClaimRemainingStagesAsync(Window window, string profileName, ProjectHandle project)
    {
        var portfolioVm = await WaitForPortfolioInvestmentAsync(window, profileName, project, investment => investment.Step == 3);
        var investment = portfolioVm.Investments.First(i => i.ProjectIdentifier == project.ProjectIdentifier);
        investment.InvestmentWalletId.Should().NotBeNullOrEmpty();
        investment.InvestmentTransactionId.Should().NotBeNullOrEmpty();

        var releaseState = await WaitForRecoveryActionAsync(portfolioVm, investment, profileName, expectedActionKey: "unfundedRelease");
        releaseState.ActionKey.Should().Be("unfundedRelease");

        Log(profileName, "Claiming remaining stages without penalty...");
        await EnsureWalletHasFeeFunds(window, profileName, investment.InvestmentWalletId, "before unfunded release");
        var releaseResult = await ExecuteActionWithRetry(
            profileName,
            "unfunded-release",
            () => portfolioVm.ReleaseFundsAsync(investment),
            () => LogUnfundedReleaseBuildDiagnostics(investment));
        releaseResult.Should().BeTrue();
    }

    private async Task<bool> ExecuteActionWithRetry(
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
            Log(profileName, $"Attempt #{attempt} for action '{actionLabel}'...");
            var result = await execute();
            if (result)
            {
                Log(profileName, $"Action '{actionLabel}' succeeded on attempt #{attempt}.");
                return true;
            }

            await logDiagnostics();
            await Task.Delay(PollInterval);
        }

        return false;
    }

    private static async Task LogUnfundedReleaseBuildDiagnostics(InvestmentViewModel investment)
    {
        var investmentAppService = global::App.App.Services.GetRequiredService<IInvestmentAppService>();
        var walletId = new WalletId(investment.InvestmentWalletId);
        var projectId = new ProjectId(investment.ProjectIdentifier);
        var feeRate = new DomainFeerate(20);

        var buildResult = await investmentAppService.BuildUnfundedReleaseTransaction(
            new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(walletId, projectId, feeRate));

        Log(investment.ProjectIdentifier, buildResult.IsSuccess
            ? $"Diagnostic BuildUnfundedReleaseTransaction succeeded. TxId={buildResult.Value.TransactionDraft.TransactionId}"
            : $"Diagnostic BuildUnfundedReleaseTransaction failed: {buildResult.Error}");
    }

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
        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

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
                Log(profileName, $"Portfolio investment found. Step={investment.Step}, Status={investment.StatusText}, WalletId={investment.InvestmentWalletId}, InvestmentTxId={investment.InvestmentTransactionId}");
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
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
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

    private async Task<ProjectItemViewModel> FindProjectFromSdkAsync(Window window, string profileName, ProjectHandle project)
    {
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findProjectsVm = GetFindProjectsViewModel(window);
        findProjectsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await findProjectsVm!.LoadProjectsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

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

    private async Task WipeExistingData(Window window, string profileName)
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

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
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

public class InvestAndRecoverTest
{
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task FullInvestAndRecoverFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(InvestAndRecoverTest));
        Log("========== STARTING FullInvestAndRecoverFlow ==========");

        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Test Invest {runId}";
        var projectAbout =
            $"Automated invest-and-recover test run {runId}. Verifies invest creation, founder spend, and investor recovery.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        var targetAmountBtc = "1.0";
        var investmentAmountBtc = "0.02";
        var investEndDate = DateTime.UtcNow.AddMonths(3);
        var installmentCount = 3;
        var investStartDate = DateTime.UtcNow.AddDays(-40).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Log($"[STEP 0] Run ID: {runId}");
        Log($"[STEP 0] Project name: {projectName}");
        Log($"[STEP 0] Investment amount: {investmentAmountBtc} BTC");

        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();

        Log("[STEP 1] Wiping existing data...");
        await WipeExistingData(window);

        Log("[STEP 2] Navigating to Funds section...");
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", UiTimeout);
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        Log("[STEP 2] Creating wallet via Generate path...");
        await CreateWalletViaGenerate(window);

        Log("[STEP 3] Waiting for WalletCard to appear...");
        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        Log("[STEP 3] Requesting testnet coins and waiting for balance...");
        await FundWalletViaFaucet(window);

        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log("[STEP 3] Set SimplePasswordProvider key to 'default-key'.");

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

        Log("[STEP 4.1] Selecting 'investment' project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("investment");
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.2] Setting project name and about...");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.3] Setting banner and profile images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.4] Setting target amount and investment end date...");
        wizardVm.TargetAmount = targetAmountBtc;
        wizardVm.InvestEndDate = investEndDate;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.5] Generating three monthly investment stages...");
        wizardVm.ShowStep5Welcome.Should().BeTrue("Step 5 should start with welcome screen");
        wizardVm.DismissStep5Welcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.DurationValue = "3";
        wizardVm.DurationUnit = "Months";
        wizardVm.ReleaseFrequency = "Monthly";
        wizardVm.StartDate = investStartDate;
        wizardVm.GenerateInvestmentStages();
        Dispatcher.UIThread.RunJobs();
        wizardVm.Stages.Count.Should().Be(installmentCount);
        wizardVm.Stages.Select(s => s.StageNumber).Should().ContainInOrder(1, 2, 3);
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log("[STEP 4.6] Deploying project...");
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

        deployVm.Wallets.Count.Should().BeGreaterThan(0, "At least one wallet should be loaded");
        var deployWallet = deployVm.Wallets[0];
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
                break;
            }

            if (!deployVm.IsDeploying && deployVm.CurrentScreen != DeployScreen.Success)
            {
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
        if (shellVm.IsModalOpen)
        {
            shellVm.HideModal();
            Dispatcher.UIThread.RunJobs();
        }

        var project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull($"Project with run ID '{runId}' should appear in My Projects");

        Log("[STEP 4.7] Reloading founder projects from SDK to populate identifiers...");
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull();
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty();
        project.OwnerWalletId.Should().NotBeNullOrEmpty();

        Log("[STEP 5] Navigating to Find Projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findProjectsVm = GetFindProjectsViewModel(window);
        findProjectsVm.Should().NotBeNull("FindProjectsViewModel should be available");

        ProjectItemViewModel? foundProject = null;
        var findDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < findDeadline)
        {
            await findProjectsVm!.LoadProjectsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            foundProject = findProjectsVm.Projects.FirstOrDefault(p =>
                p.Description.Contains(runId) || p.ShortDescription.Contains(runId));
            if (foundProject != null)
                break;

            await Task.Delay(PollInterval);
        }

        foundProject.Should().NotBeNull($"Should find our project (run ID '{runId}') in Find Projects from SDK");
        foundProject!.ProjectName.Should().Be(projectName);
        foundProject.ProjectType.Should().Be("Invest");
        foundProject.Target.Should().Be("1.00000");
        foundProject.ProjectId.Should().NotBeNullOrEmpty();

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

        var investWalletDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < investWalletDeadline && investVm!.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }

        investVm!.Wallets.Count.Should().BeGreaterThan(0);
        investVm.Stages.Count.Should().Be(installmentCount, "invest project should expose fixed stages");
        investVm.InvestmentAmount = investmentAmountBtc;
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue();

        Log("[STEP 6] Submitting invest form...");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var investWallet = investVm.Wallets[0];
        investVm.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();
        investVm.SelectedWallet.Should().NotBeNull();

        Log("[STEP 6] Paying with wallet (SDK invest pipeline)...");
        investVm.PayWithWallet();

        var investDeadline = DateTime.UtcNow + TransactionTimeout;
        while (DateTime.UtcNow < investDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                break;
            }

            if (!investVm.IsProcessing && investVm.CurrentScreen != InvestScreen.Success)
            {
                if (investVm.PaymentStatusText.Contains("Failed") || investVm.PaymentStatusText.Contains("Error"))
                    break;
            }

            await Task.Delay(PollInterval);
        }

        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"Invest should reach success. Last status: {investVm.PaymentStatusText}");

        Log("[STEP 7] Adding investment to portfolio...");
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        portfolioVm.HasInvestments.Should().BeTrue();

        Log("[STEP 7.1] Verifying no duplicate investments after SDK reload...");
        await portfolioVm.LoadInvestmentsFromSdkAsync();
        Dispatcher.UIThread.RunJobs();

        var matchingInvestments = portfolioVm.Investments
            .Where(i => i.ProjectIdentifier == foundProject.ProjectId || i.ProjectName == foundProject.ProjectName)
            .ToList();
        matchingInvestments.Count.Should().Be(1,
            "There should be exactly one investment entry for our project after AddToPortfolio + SDK reload");

        var localInvestment = matchingInvestments[0];
        localInvestment.ProjectType.Should().Be("invest");
        localInvestment.TypeLabel.Should().Be("Investment");
        localInvestment.Step.Should().Be(1, "Invest-type projects should wait for founder approval");
        localInvestment.StatusText.Should().Be("Awaiting Approval");
        localInvestment.ApprovalStatus.Should().Be("Pending");

        Log("[STEP 8] Founder approving pending investment request...");
        findProjectsVm.CloseInvestPage();
        findProjectsVm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        window.NavigateToSection("Funders");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var fundersVm = GetFundersViewModel(window);
        fundersVm.Should().NotBeNull();
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
                break;

            await Task.Delay(PollInterval);
        }

        pendingRequest.Should().NotBeNull();
        fundersVm.ApproveSignature(pendingRequest!.Id);
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
                break;

            await Task.Delay(PollInterval);
        }

        founderApproved.Should().BeTrue();

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

            if (signedInvestment is { Step: 2 } || signedInvestment?.ApprovalStatus == "Approved")
                break;

            await Task.Delay(PollInterval);
        }

        signedInvestment.Should().NotBeNull();
        signedInvestment!.Step.Should().Be(2);
        signedInvestment.ProjectType.Should().Be("invest");

        var confirmResult = await portfolioVm.ConfirmInvestmentAsync(signedInvestment);
        Dispatcher.UIThread.RunJobs();
        confirmResult.Should().BeTrue();
        signedInvestment.Step.Should().Be(3);
        signedInvestment.StatusText.Should().Be("Investment Active");
        signedInvestment.StatusClass.Should().Be("active");

        Log("[STEP 10] Founder spending stage 1...");
        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var founderProjectsVm = GetMyProjectsViewModel(window);
        founderProjectsVm.Should().NotBeNull();
        founderProjectsVm!.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var manageVm = founderProjectsVm.SelectedManageProject;
        manageVm.Should().NotBeNull();

        Log("[STEP 10.0] Verifying ManageProject stages before founder spend...");
        var preSpendDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < preSpendDeadline)
        {
            await manageVm!.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var stage1 = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.CanClaim);
            if (manageVm.Stages.Count == installmentCount && stage1 != null)
                break;

            await Task.Delay(PollInterval);
        }

        manageVm!.Stages.Count.Should().Be(installmentCount);
        manageVm.TotalStages.Should().Be(installmentCount);

        var stage1Pre = manageVm.Stages.First(s => s.Number == 1);
        var laterStages = manageVm.Stages.Where(s => s.Number > 1).OrderBy(s => s.Number).ToList();

        stage1Pre.Available.Should().BeTrue();
        stage1Pre.CanClaim.Should().BeTrue("first stage should already be claimable");
        stage1Pre.AvailableTransactions.Count.Should().BeGreaterThan(0);
        stage1Pre.SpentTransactionCount.Should().Be(0);
        stage1Pre.UnspentTransactionCount.Should().BeGreaterThan(0);
        stage1Pre.UtxoCount.Should().Be(stage1Pre.UnspentTransactionCount,
            "stage 1 should only have unspent UTXOs before founder claim");
        stage1Pre.ButtonMode.Should().Be("Claim");
        stage1Pre.ShowAvailableInDays.Should().BeFalse();
        stage1Pre.DaysUntilAvailable.Should().BeNull("stage 1 should already be released");
        stage1Pre.AmountLeft.Should().NotBe("0.00000000");
        stage1Pre.CompletionDate.Should().NotBeNullOrEmpty();

        laterStages.Should().HaveCount(2);
        var stage2Pre = laterStages[0];
        var stage3Pre = laterStages[1];

        laterStages.Should().AllSatisfy(stage =>
        {
            stage.Available.Should().BeTrue();
            stage.CanClaim.Should().BeFalse("later stages should still be locked before recovery");
            stage.DaysUntilAvailable.Should().NotBeNull();
            stage.DaysUntilAvailable.Should().BeGreaterThan(0);
            stage.UnspentTransactionCount.Should().BeGreaterThan(0);
            stage.UtxoCount.Should().Be(stage.UnspentTransactionCount,
                $"stage {stage.Number} should only have unspent locked UTXOs before founder claim");
            stage.SpentTransactionCount.Should().Be(0);
            stage.ButtonMode.Should().Be("AvailableInDays");
            stage.ShowAvailableInDays.Should().BeTrue();
            stage.AmountLeft.Should().NotBe("0.00000000");
            stage.CompletionDate.Should().NotBeNullOrEmpty();
        });

        stage2Pre.DaysUntilAvailable.Should().BeInRange(10, 35,
            "stage 2 should be the next monthly release and still some days away");
        stage3Pre.DaysUntilAvailable.Should().BeInRange(35, 65,
            "stage 3 should be farther out than stage 2");
        stage3Pre.DaysUntilAvailable.Should().BeGreaterThan(stage2Pre.DaysUntilAvailable!.Value,
            "stage 3 countdown should be greater than stage 2 countdown");

        var claimableDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < claimableDeadline)
        {
            await manageVm.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            var claimableStage = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.AvailableTransactions.Count > 0);
            if (claimableStage != null)
            {
                var claimResult = await manageVm.ClaimStageFundsAsync(claimableStage.Number, claimableStage.AvailableTransactions.ToList());
                Dispatcher.UIThread.RunJobs();
                claimResult.Should().BeTrue();
                break;
            }

            await Task.Delay(PollInterval);
        }

        var spentStageDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < spentStageDeadline)
        {
            await manageVm.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            if (manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0))
                break;

            await Task.Delay(PollInterval);
        }

        manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0).Should().BeTrue();

        Log("[STEP 11] Navigating to Funded section...");
        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var investment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName || i.ProjectIdentifier == foundProject.ProjectId);
        investment.Should().NotBeNull();

        await portfolioVm.LoadInvestmentsFromSdkAsync();
        Dispatcher.UIThread.RunJobs();

        var sdkInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName || i.ProjectIdentifier == foundProject.ProjectId);
        var targetInvestment = sdkInvestment ?? investment;
        targetInvestment.Should().NotBeNull();

        Log("[STEP 12] Loading recovery status...");
        if (string.IsNullOrEmpty(targetInvestment!.InvestmentWalletId))
            targetInvestment.InvestmentWalletId = investWallet.Id.Value;
        if (string.IsNullOrEmpty(targetInvestment.ProjectIdentifier))
            targetInvestment.ProjectIdentifier = foundProject.ProjectId;

        await Task.Delay(TimeSpan.FromSeconds(30));

        var hasRecoveryAction = false;
        var recoveryDeadline = DateTime.UtcNow + IndexerLagTimeout;
        while (DateTime.UtcNow < recoveryDeadline)
        {
            await portfolioVm.LoadRecoveryStatusAsync(targetInvestment);
            Dispatcher.UIThread.RunJobs();

            if (targetInvestment.RecoveryState.HasAction)
            {
                hasRecoveryAction = true;
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        hasRecoveryAction.Should().BeTrue();

        Log("[STEP 12.1] Verifying recovery stage display...");
        targetInvestment.Stages.Count.Should().Be(installmentCount);

        foreach (var stage in targetInvestment.Stages)
        {
            stage.StageNumber.Should().BeGreaterThan(0);
            stage.Amount.Should().NotBe("0.00000000");
            stage.Status.Should().BeOneOf("Spent by founder", "Not Spent", "Pending");
            Log($"[STEP 12.1] Recovery Stage #{stage.StageNumber}: amount={stage.Amount}, status='{stage.Status}'");
        }

        var recoveryStage1 = targetInvestment.Stages.FirstOrDefault(s => s.StageNumber == 1);
        recoveryStage1.Should().NotBeNull();
        recoveryStage1!.Status.Should().Be("Spent by founder");

        var recoveryLaterStages = targetInvestment.Stages.Where(s => s.StageNumber > 1).ToList();
        recoveryLaterStages.Should().HaveCount(2);
        recoveryLaterStages.Should().AllSatisfy(s =>
            s.Status.Should().Be("Not Spent"));

        targetInvestment.ProjectType.Should().Be("invest");
        targetInvestment.ShowRecoverButton.Should().BeTrue();
        targetInvestment.StagesToRecover.Should().BeGreaterThan(0);
        double.TryParse(targetInvestment.AmountToRecover, NumberStyles.Float, CultureInfo.InvariantCulture, out var amountToRecover)
            .Should().BeTrue();
        amountToRecover.Should().BeGreaterThan(0);

        var actionKey = targetInvestment.RecoveryState.ActionKey;
        Log($"[STEP 12] Executing recovery action: '{actionKey}' ({targetInvestment.RecoveryState.ButtonLabel})...");

        await EnsureWalletHasFeeFunds(window, targetInvestment.InvestmentWalletId, "before recovery action");
        var recoveryResult = await ExecuteRecoveryActionWithRetry(portfolioVm, targetInvestment, actionKey);

        Dispatcher.UIThread.RunJobs();

        Log($"[STEP 13] Recovery result: {recoveryResult}");
        recoveryResult.Should().BeTrue(
            $"Recovery operation '{actionKey}' should succeed (transaction built and published)");

        window.Close();
        Log("========== FullInvestAndRecoverFlow PASSED ==========");
    }

    private async Task WipeExistingData(Window window)
    {
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants()
            .OfType<SettingsView>()
            .FirstOrDefault();

        if (settingsView?.DataContext is SettingsViewModel settingsVm)
        {
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private async Task CreateWalletViaGenerate(Window window)
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
    }

    private async Task FundWalletViaFaucet(Window window)
    {
        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();

        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty();

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (fundsVm.TotalBalance != "0.0000")
                return;

            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                lastFaucetAttempt = DateTime.UtcNow;
                await fundsVm.GetTestCoinsAsync(walletId!);
                Dispatcher.UIThread.RunJobs();
            }

            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        fundsVm.TotalBalance.Should().NotBe("0.0000");
    }

    private async Task OpenCreateWizard(Window window, MyProjectsViewModel myProjectsVm)
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
    }

    private static Button? FindAddWalletButton(Window window)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible);

        foreach (var btn in buttons)
        {
            if (btn.Content is string text && text.Contains("Add Wallet"))
                return btn;

            if (btn.Content is StackPanel panel)
            {
                foreach (var child in panel.Children.OfType<TextBlock>())
                {
                    if (child.Text == "Add Wallet")
                        return btn;
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

    private async Task<bool> ExecuteRecoveryActionWithRetry(
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        string actionKey)
    {
        var deadline = DateTime.UtcNow + IndexerLagTimeout;
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            Log($"[STEP 12] Recovery attempt #{attempt} for action '{actionKey}'...");

            bool result = actionKey switch
            {
                "recovery" => await portfolioVm.RecoverFundsAsync(investment),
                "unfundedRelease" => await portfolioVm.ReleaseFundsAsync(investment),
                "endOfProject" => await portfolioVm.ClaimEndOfProjectAsync(investment),
                "penaltyRelease" => await portfolioVm.PenaltyReleaseFundsAsync(investment),
                _ => false
            };

            if (result)
                return true;

            await portfolioVm.LoadRecoveryStatusAsync(investment);
            Dispatcher.UIThread.RunJobs();
            Log($"[STEP 12] Retry state: ActionKey={investment.RecoveryState.ActionKey}, " +
                $"HasUnspent={investment.RecoveryState.HasUnspentItems}, " +
                $"EndOfProject={investment.RecoveryState.EndOfProject}, " +
                $"HasReleaseSig={investment.RecoveryState.HasReleaseSignatures}, " +
                $"InPenalty={investment.RecoveryState.HasSpendableItemsInPenalty}");
            await Task.Delay(PollInterval);
        }

        return false;
    }

    private async Task EnsureWalletHasFeeFunds(Window window, string walletId, string context)
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
                var availableBtc = available / 100_000_000m;
                Log($"[STEP 12] Wallet balance {context}: {availableBtc:F8} BTC available for fees");
                if (available > 20_000)
                    return;
            }

            Log($"[STEP 12] Wallet needs fee funds {context}. Requesting faucet coins...");
            await fundsVm!.GetTestCoinsAsync(walletId);
            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        throw new InvalidOperationException($"Wallet '{walletId}' did not receive enough fee funds {context}.");
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

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

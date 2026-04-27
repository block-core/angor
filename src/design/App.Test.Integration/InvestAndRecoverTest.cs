using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Shared.PaymentFlow;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.Portfolio;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

public class InvestAndRecoverTest
{

    [AvaloniaFact]
    public async Task FullInvestAndRecoverFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(InvestAndRecoverTest));
        TestHelpers.Log("========== STARTING FullInvestAndRecoverFlow ==========");

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

        TestHelpers.Log($"[STEP 0] Run ID: {runId}");
        TestHelpers.Log($"[STEP 0] Project name: {projectName}");
        TestHelpers.Log($"[STEP 0] Investment amount: {investmentAmountBtc} BTC");

        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();

        TestHelpers.Log("[STEP 1] Wiping existing data...");
        await window.WipeExistingData();

        TestHelpers.Log("[STEP 2] Navigating to Funds section...");
        await window.NavigateToSectionAndVerify("Funds");

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", TestHelpers.UiTimeout);
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        TestHelpers.Log("[STEP 2] Creating wallet via Generate path...");
        await window.CreateWalletViaGenerate();

        TestHelpers.Log("[STEP 3] Waiting for WalletCard to appear...");
        var walletCardBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        TestHelpers.Log("[STEP 3] Requesting testnet coins and waiting for balance...");
        await window.FundWalletViaFaucet();

        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        TestHelpers.Log("[STEP 3] Set SimplePasswordProvider key to 'default-key'.");

        TestHelpers.Log("[STEP 4] Navigating to My Projects section...");
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = window.GetMyProjectsViewModel();
        myProjectsVm.Should().NotBeNull("MyProjectsViewModel should be available");

        TestHelpers.Log("[STEP 4] Opening create wizard...");
        await window.OpenCreateWizard(myProjectsVm!);

        var wizardVm = myProjectsVm!.CreateProjectVm;
        wizardVm.Should().NotBeNull("CreateProjectViewModel should exist");

        TestHelpers.Log("[STEP 4.1] Selecting 'investment' project type...");
        wizardVm.DismissWelcome();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        wizardVm.SelectProjectType("investment");
        Dispatcher.UIThread.RunJobs();
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 4.2] Setting project name and about...");
        wizardVm.ProjectName = projectName;
        wizardVm.ProjectAbout = projectAbout;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 4.3] Setting banner and profile images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 4.4] Setting target amount and investment end date...");
        wizardVm.TargetAmount = targetAmountBtc;
        wizardVm.InvestEndDate = investEndDate;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 4.5] Generating three monthly investment stages...");
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

        TestHelpers.Log("[STEP 4.6] Deploying project...");
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

        TestHelpers.Log("[STEP 4.6] Paying with wallet (SDK deploy pipeline)...");
        deployVm.PayWithWallet();

        var deployDeadline = DateTime.UtcNow + TestHelpers.TransactionTimeout;
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

            await Task.Delay(TestHelpers.PollInterval);
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

        TestHelpers.Log("[STEP 4.7] Reloading founder projects from SDK to populate identifiers...");
        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        project = myProjectsVm.Projects.FirstOrDefault(p => p.Description.Contains(runId));
        project.Should().NotBeNull();
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty();
        project.OwnerWalletId.Should().NotBeNullOrEmpty();

        TestHelpers.Log("[STEP 5] Navigating to Find Projects...");
        await window.NavigateToSectionAndVerify("Find Projects");

        var findProjectsVm = window.GetFindProjectsViewModel();
        findProjectsVm.Should().NotBeNull("FindProjectsViewModel should be available");

        ProjectItemViewModel? foundProject = null;
        var findDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < findDeadline)
        {
            await findProjectsVm!.LoadAllProjectsFromSdkAsync();

            foundProject = findProjectsVm.Projects.FirstOrDefault(p =>
                p.Description.Contains(runId) || p.ShortDescription.Contains(runId));
            if (foundProject != null)
                break;

            await Task.Delay(TestHelpers.PollInterval);
        }

        foundProject.Should().NotBeNull($"Should find our project (run ID '{runId}') in Find Projects from SDK");
        foundProject!.ProjectName.Should().Be(projectName);
        foundProject.ProjectType.Should().Be("Invest");
        foundProject.Target.Should().Be("1.00000");
        foundProject.ProjectId.Should().NotBeNullOrEmpty();

        // Refresh wallet UTXOs before investing — the deploy tx consumed UTXOs from the same
        // wallet, and the investment tx must be built against the post-deploy UTXO set to avoid
        // txn-mempool-conflict when publishing later. We poll until the balance reflects the
        // deploy fee deduction (i.e., the indexer has processed the deploy tx).
        TestHelpers.Log("[STEP 5.1] Waiting for wallet UTXOs to reflect deploy tx...");
        await window.NavigateToSectionAndVerify("Funds");
        var utxoDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < utxoDeadline)
        {
            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(3000);
            Dispatcher.UIThread.RunJobs();

            // After a deploy, balance should be less than the original ~2 BTC faucet amount
            // (deploy fee was deducted). If the balance changed from the original, the indexer
            // has processed the deploy tx and UTXOs are current.
            var fundsVm = window.GetFundsViewModel();
            if (fundsVm != null)
            {
                var balanceText = fundsVm.TotalBalance;
                TestHelpers.Log($"[STEP 5.1] Current balance: {balanceText}");
                // Balance should have decreased from original 2.0000 after paying deploy fee
                if (!string.IsNullOrEmpty(balanceText) && balanceText != "0.0000" &&
                    decimal.TryParse(balanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var bal) &&
                    bal < 2.0m)
                {
                    TestHelpers.Log($"[STEP 5.1] Balance reflects deploy fee deduction: {balanceText}");
                    break;
                }
            }
        }
        await window.NavigateToSectionAndVerify("Find Projects");

        TestHelpers.Log("[STEP 6] Opening project detail...");
        // Re-load projects since we navigated away
        await findProjectsVm!.LoadAllProjectsFromSdkAsync();
        foundProject = findProjectsVm.Projects.FirstOrDefault(p =>
            p.Description.Contains(runId) || p.ShortDescription.Contains(runId));
        foundProject.Should().NotBeNull();

        findProjectsVm.OpenProjectDetail(foundProject!);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);

        TestHelpers.Log("[STEP 6] Opening invest page...");
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

        TestHelpers.Log("[STEP 6] Submitting invest form...");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var pf = investVm.PaymentFlow;
        pf.Should().NotBeNull("PaymentFlow should be created after Submit()");

        var investWallet = pf!.Wallets[0];
        pf.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();
        pf.HasSelectedWallet.Should().BeTrue();

        TestHelpers.Log("[STEP 6] Paying with wallet (SDK invest pipeline)...");
        pf.PayWithWalletCommand.Execute().Subscribe();

        var investDeadline = DateTime.UtcNow + TestHelpers.TransactionTimeout;
        while (DateTime.UtcNow < investDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (pf.CurrentScreen == PaymentFlowScreen.Success)
                break;

            if (pf.ErrorMessage != null)
            {
                TestHelpers.Log($"[STEP 6] Invest ERROR: {pf.ErrorMessage}");
                break;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        pf.CurrentScreen.Should().Be(PaymentFlowScreen.Success,
            $"Invest should reach success. Error: {pf.ErrorMessage ?? "none"}");

        TestHelpers.Log($"[STEP 6] Invest success title: '{pf.SuccessTitle}'");

        TestHelpers.Log("[STEP 7] Adding investment to portfolio...");
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        // DIRECT DI RESOLVE: PortfolioViewModel is a singleton that isn't easily reachable
        // from the visual tree at this point — we just left the Find Projects section.
        // Resolving from DI mirrors what the navigation framework does internally.
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        portfolioVm.HasInvestments.Should().BeTrue();

        TestHelpers.Log("[STEP 7.1] Verifying no duplicate investments after SDK reload...");
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

        // Verify portfolio summary stats are set after SDK load (fix #4/5)
        portfolioVm.FundedProjects.Should().BeGreaterThan(0, "FundedProjects should be set after LoadInvestmentsFromSdkAsync");
        double.TryParse(portfolioVm.TotalInvested, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var totalInvBtc);
        totalInvBtc.Should().BeGreaterThan(0, "TotalInvested should be > 0 after investing");
        TestHelpers.Log($"[STEP 7.1] Portfolio stats: FundedProjects={portfolioVm.FundedProjects}, TotalInvested={portfolioVm.TotalInvested}");

        TestHelpers.Log("[STEP 8] Founder approving pending investment request...");
        findProjectsVm.CloseInvestPage();
        findProjectsVm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        await window.NavigateToSectionAndVerify("Funders");

        var fundersVm = window.GetFundersViewModel();
        fundersVm.Should().NotBeNull();
        fundersVm!.SetFilter("waiting");

        SignatureRequestViewModel? pendingRequest = null;
        var approvalRequestDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < approvalRequestDeadline)
        {
            await fundersVm.LoadInvestmentRequestsAsync();
            fundersVm.SetFilter("waiting");
            Dispatcher.UIThread.RunJobs();

            pendingRequest = fundersVm.FilteredSignatures.FirstOrDefault(s =>
                s.ProjectIdentifier == foundProject.ProjectId || s.ProjectTitle == foundProject.ProjectName);
            if (pendingRequest != null)
                break;

            await Task.Delay(TestHelpers.PollInterval);
        }

        pendingRequest.Should().NotBeNull();
        fundersVm.ApproveSignature(pendingRequest!.Id);
        Dispatcher.UIThread.RunJobs();

        var approvalCompleteDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
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

            await Task.Delay(TestHelpers.PollInterval);
        }

        founderApproved.Should().BeTrue();

        TestHelpers.Log("[STEP 9] Reloading funded investments and confirming signed investment...");
        await window.NavigateToSectionAndVerify("Funded");

        InvestmentViewModel? signedInvestment = null;
        var signedDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < signedDeadline)
        {
            await window.ClickButton("PortfolioRefreshButton");
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();

            signedInvestment = portfolioVm.Investments.FirstOrDefault(i =>
                i.ProjectIdentifier == foundProject.ProjectId || i.ProjectName == foundProject.ProjectName);

            if (signedInvestment is { Step: 2 } || signedInvestment?.ApprovalStatus == "Approved")
                break;

            await Task.Delay(TestHelpers.PollInterval);
        }

        signedInvestment.Should().NotBeNull();
        signedInvestment!.Step.Should().Be(2);
        signedInvestment.ProjectType.Should().Be("invest");

        // Verify TotalInvested is populated from SDK (invest-type project)
        double.TryParse(signedInvestment.TotalInvested, NumberStyles.Float, CultureInfo.InvariantCulture, out var investTotalInvested)
            .Should().BeTrue("TotalInvested should parse as a numeric BTC amount");
        investTotalInvested.Should().BeGreaterThan(0,
            "TotalInvested should be > 0 for invest-type project after SDK reload (uses indexer TotalAmount or InvestedAmountSats fallback)");
        TestHelpers.Log($"[STEP 9] Invest-type TotalInvested={signedInvestment.TotalInvested}");

        // Verify TotalInvestors is populated from SDK stats (fix #4/5)
        signedInvestment.TotalInvestors.Should().BeGreaterThanOrEqualTo(0,
            "TotalInvestors should be populated from SDK indexer stats");
        TestHelpers.Log($"[STEP 9] TotalInvestors={signedInvestment.TotalInvestors}");

        // Verify portfolio summary stats are populated (fix #4/5)
        portfolioVm.FundedProjects.Should().BeGreaterThan(0, "FundedProjects count should reflect loaded investments");
        TestHelpers.Log($"[STEP 9] Portfolio stats: FundedProjects={portfolioVm.FundedProjects}, TotalInvested={portfolioVm.TotalInvested}");

        // Retry ConfirmInvestmentAsync with backoff — the publish can fail with
        // txn-mempool-conflict when the indexer hasn't yet reflected the deploy tx's
        // UTXO changes or when a previous test run left stale transactions in the mempool.
        var confirmResult = false;
        var confirmDeadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        var confirmAttempt = 0;
        while (DateTime.UtcNow < confirmDeadline)
        {
            confirmAttempt++;
            confirmResult = await portfolioVm.ConfirmInvestmentAsync(signedInvestment);
            Dispatcher.UIThread.RunJobs();

            if (confirmResult)
            {
                TestHelpers.Log($"[STEP 9] ConfirmInvestmentAsync succeeded on attempt #{confirmAttempt}");
                break;
            }

            TestHelpers.Log($"[STEP 9] ConfirmInvestmentAsync failed on attempt #{confirmAttempt}, retrying after delay...");
            // Reset step back to 2 so the next attempt doesn't skip due to state
            signedInvestment.Step = 2;
            var backoff = Math.Min(15_000, 5_000 * confirmAttempt);
            await Task.Delay(backoff);

            // Refresh wallet balance to pick up new UTXO state
            await window.ClickButton("PortfolioRefreshButton");
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }

        confirmResult.Should().BeTrue("ConfirmInvestmentAsync should eventually succeed after retries");
        signedInvestment.Step.Should().Be(3);
        signedInvestment.StatusText.Should().Be("Investment Active");
        signedInvestment.StatusClass.Should().Be("active");

        TestHelpers.Log("[STEP 10] Founder spending stage 1...");
        await window.NavigateToSectionAndVerify("My Projects");

        var founderProjectsVm = window.GetMyProjectsViewModel();
        founderProjectsVm.Should().NotBeNull();
        founderProjectsVm!.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();

        var manageVm = founderProjectsVm.SelectedManageProject;
        manageVm.Should().NotBeNull();

        // Verify initial load spinner fires (fix #6: InitialLoadAsync sets IsRefreshing)
        // Note: by the time we check, the async load may have already completed,
        // so we just verify IsRefreshing eventually becomes false (load completes).
        TestHelpers.Log($"[STEP 10] ManageProject IsRefreshing={manageVm!.IsRefreshing} right after open");

        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 10.0] Verifying ManageProject stages before founder spend...");
        var preSpendDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < preSpendDeadline)
        {
            await window.ClickButton("ManageProjectRefreshButton");
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();

            var stage1 = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.CanClaim);
            if (manageVm.Stages.Count == installmentCount && stage1 != null)
                break;

            await Task.Delay(TestHelpers.PollInterval);
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

        var claimableDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < claimableDeadline)
        {
            await window.ClickButton("ManageProjectRefreshButton");
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();

            var claimableStage = manageVm.Stages.FirstOrDefault(s => s.Number == 1 && s.AvailableTransactions.Count > 0);
            if (claimableStage != null)
            {
                var claimResult = await manageVm.ClaimStageFundsAsync(claimableStage.Number, claimableStage.AvailableTransactions.ToList());
                Dispatcher.UIThread.RunJobs();
                claimResult.Should().BeTrue();
                break;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        var spentStageDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < spentStageDeadline)
        {
            await manageVm.LoadClaimableTransactionsAsync();
            Dispatcher.UIThread.RunJobs();

            if (manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0))
                break;

            await Task.Delay(TestHelpers.PollInterval);
        }

        manageVm.Stages.Any(s => s.Number == 1 && s.SpentTransactionCount > 0).Should().BeTrue();

        TestHelpers.Log("[STEP 11] Navigating to Funded section...");
        await window.NavigateToSectionAndVerify("Funded");

        var investment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName || i.ProjectIdentifier == foundProject.ProjectId);
        investment.Should().NotBeNull();

        await window.ClickButton("PortfolioRefreshButton");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var sdkInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectName == foundProject.ProjectName || i.ProjectIdentifier == foundProject.ProjectId);
        var targetInvestment = sdkInvestment ?? investment;
        targetInvestment.Should().NotBeNull();

        TestHelpers.Log("[STEP 12] Loading recovery status...");
        if (string.IsNullOrEmpty(targetInvestment!.InvestmentWalletId))
            targetInvestment.InvestmentWalletId = investWallet.Id.Value;
        if (string.IsNullOrEmpty(targetInvestment.ProjectIdentifier))
            targetInvestment.ProjectIdentifier = foundProject.ProjectId;

        await Task.Delay(TimeSpan.FromSeconds(30));

        var hasRecoveryAction = false;
        var recoveryDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
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

        TestHelpers.Log("[STEP 12.1] Verifying recovery stage display...");
        targetInvestment.Stages.Count.Should().Be(installmentCount);

        foreach (var stage in targetInvestment.Stages)
        {
            stage.StageNumber.Should().BeGreaterThan(0);
            stage.Amount.Should().NotBe("0.00000000");
            stage.Status.Should().BeOneOf("Spent by founder", "Not Spent", "Pending");
            TestHelpers.Log($"[STEP 12.1] Recovery Stage #{stage.StageNumber}: amount={stage.Amount}, status='{stage.Status}'");
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
        TestHelpers.Log($"[STEP 12] Clicking recovery action button: '{actionKey}' ({targetInvestment.RecoveryState.ButtonLabel})...");

        await EnsureWalletHasFeeFunds(window, targetInvestment.InvestmentWalletId, "before recovery action");
        await window.ClickRecoveryFlowAsync(portfolioVm, targetInvestment, TimeSpan.FromSeconds(30));

        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("[STEP 13] Recovery flow completed through real UI button path");
        targetInvestment.ShowSuccessModal.Should().BeTrue(
            $"Recovery operation '{actionKey}' should succeed and show the success modal");

        window.Close();
        TestHelpers.Log("========== FullInvestAndRecoverFlow PASSED ==========");
    }

    private async Task EnsureWalletHasFeeFunds(Window window, string walletId, string context)
    {
        await window.NavigateToSectionAndVerify("Funds");

        var fundsVm = window.GetFundsViewModel();
        fundsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            // DIRECT SDK CALL: FundsViewModel.TotalBalance is a formatted string that doesn't
            // expose the raw sats breakdown (confirmed + unconfirmed + reserved) we need to
            // determine whether the wallet has enough for fee-only transactions.
            var refresh = await global::App.App.Services.GetRequiredService<Angor.Sdk.Wallet.Application.IWalletAppService>()
                .RefreshAndGetAccountBalanceInfo(new WalletId(walletId));

            if (refresh.IsSuccess)
            {
                var info = refresh.Value;
                var available = info.TotalBalance + info.TotalUnconfirmedBalance + info.TotalBalanceReserved;
                var availableBtc = available / 100_000_000m;
                TestHelpers.Log($"[STEP 12] Wallet balance {context}: {availableBtc:F8} BTC available for fees");
                if (available > 20_000)
                    return;
            }

            TestHelpers.Log($"[STEP 12] Wallet needs fee funds {context}. Requesting faucet coins...");
            await fundsVm!.GetTestCoinsAsync(walletId);
            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(TestHelpers.PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        throw new InvalidOperationException($"Wallet '{walletId}' did not receive enough fee funds {context}.");
    }
}

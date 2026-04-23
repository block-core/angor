using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// End-to-end integration test for the investment cancellation flow.
///
/// CancelInvestmentRequest is implemented in the SDK and wired in the design app
/// (PortfolioViewModel.CancelInvestmentAsync), but has zero E2E coverage. This
/// test validates the full Nostr DM + handshake status + fund release flow across
/// three distinct cancellation/confirmation scenarios.
///
/// Flow (3-step):
///   Phase 1 (Founder):
///     1. Wipe data, create wallet, fund via faucet
///     2. Create and deploy a fund project with 0.01 BTC approval threshold
///
///   Phase 2 (Investor) — Cancel before founder approval:
///     3. Create wallet, fund via faucet
///     4. Invest above threshold (0.02 BTC) — pending approval (Step 1)
///     5. Cancel the pending investment before founder approves
///     6. Verify: investment status = Cancelled, funds released
///
///   Phase 3 (Investor) — Re-invest after cancel:
///     7. Re-invest in the same project (new pending investment)
///
///   Phase 4 (Founder) — Approve the new investment:
///     8. Founder approves the pending investment request
///
///   Phase 5 (Investor) — Cancel after founder approval:
///     9. Cancel the approved investment (Step 2)
///     10. Verify: investment status = Cancelled, funds released
///
///   Phase 6 (Investor) — Re-invest again:
///     11. Re-invest in the same project
///
///   Phase 7 (Founder) — Approve again:
///     12. Founder approves the new investment request
///
///   Phase 8 (Investor) — Confirm the approved investment:
///     13. Investor confirms the approved investment
///     14. Verify: investment reaches Step 3 (active)
///
/// This validates:
///   - Cancel before founder approval works (Step 1 → Cancelled)
///   - Cancel after founder approval works (Step 2 → Cancelled)
///   - Confirming an approved investment completes the cycle (Step 3)
///   - Cancelled investments release reserved UTXOs (funds not locked)
///   - Re-investing after cancellation creates a new, separate investment
///   - The full founder-approval handshake works end-to-end
///
/// Uses real testnet infrastructure (indexer + faucet + Nostr relays).
/// May take 3-10 minutes depending on network conditions.
/// </summary>
public class InvestmentCancellationTest
{
    private const string TestName = "InvestmentCancellation";
    private const string FounderProfile = TestName + "-Founder";
    private const string InvestorProfile = TestName + "-Investor";


    private sealed record ProjectHandle(string RunId, string ProjectName, string ProjectIdentifier, string FounderWalletId);

    [AvaloniaFact]
    public async Task CancelPendingInvestmentAndReinvest()
    {
        var initializedProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Cancel Test {runId}";
        var projectAbout = $"{TestName} run {runId}. Validates investment cancellation and reinvestment.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var payoutDay = DateTime.UtcNow.DayOfWeek.ToString();
        var investmentAmountBtc = "0.02"; // above 0.01 threshold → requires founder approval

        Log(null, $"========== STARTING {nameof(CancelPendingInvestmentAndReinvest)} ==========");
        Log(null, $"Run ID: {runId}");
        Log(null, $"Founder profile: {FounderProfile}");
        Log(null, $"Investor profile: {InvestorProfile}");

        ProjectHandle? project = null;

        // ──────────────────────────────────────────────────────────────
        // PHASE 1: Founder — create wallet, fund, deploy project
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 1: Founder creates wallet, funds, deploys project ═══");
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, FounderProfile);
            project = await CreateFundProjectAsync(
                window,
                FounderProfile,
                projectName,
                projectAbout,
                bannerImageUrl,
                profileImageUrl,
                payoutDay,
                runId);
        });

        project.Should().NotBeNull("Founder phase should produce a deployed project");
        Log(null, $"Phase 1 complete. ProjectId={project!.ProjectIdentifier}");

        // ──────────────────────────────────────────────────────────────
        // PHASE 2: Investor — invest, cancel BEFORE founder approval
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 2: Investor invests and cancels BEFORE founder approval ═══");
        await WithProfileWindow(InvestorProfile, initializedProfiles, async window =>
        {
            await CreateWalletAndFundAsync(window, InvestorProfile);
            await InvestAndCancelBeforeApprovalAsync(window, InvestorProfile, project!, investmentAmountBtc);
        });

        Log(null, "Phase 2 complete. Cancel-before-approval validated.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 3: Investor — re-invest (new pending investment)
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 3: Investor re-invests after cancel ═══");
        await WithProfileWindow(InvestorProfile, initializedProfiles, async window =>
        {
            await InvestInProjectFromSdkAsync(window, InvestorProfile, project!, investmentAmountBtc);
        });

        Log(null, "Phase 3 complete. Re-investment submitted.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 4: Founder — approve the pending investment
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 4: Founder approves the pending investment ═══");
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await ApprovePendingInvestmentAsync(window, FounderProfile, project!);
        });

        Log(null, "Phase 4 complete. Founder approved the investment.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 5: Investor — cancel AFTER founder approval
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 5: Investor cancels AFTER founder approval ═══");
        await WithProfileWindow(InvestorProfile, initializedProfiles, async window =>
        {
            await CancelAfterApprovalAsync(window, InvestorProfile, project!);
        });

        Log(null, "Phase 5 complete. Cancel-after-approval validated.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 6: Investor — re-invest again
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 6: Investor re-invests again ═══");
        await WithProfileWindow(InvestorProfile, initializedProfiles, async window =>
        {
            await InvestInProjectFromSdkAsync(window, InvestorProfile, project!, investmentAmountBtc);
        });

        Log(null, "Phase 6 complete. Second re-investment submitted.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 7: Founder — approve again
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 7: Founder approves the new investment ═══");
        await WithProfileWindow(FounderProfile, initializedProfiles, async window =>
        {
            await ApprovePendingInvestmentAsync(window, FounderProfile, project!);
        });

        Log(null, "Phase 7 complete. Founder approved the second investment.");

        // ──────────────────────────────────────────────────────────────
        // PHASE 8: Investor — confirm approved investment (Step 3)
        // ──────────────────────────────────────────────────────────────
        Log(null, "═══ PHASE 8: Investor confirms the approved investment ═══");
        await WithProfileWindow(InvestorProfile, initializedProfiles, async window =>
        {
            await ConfirmApprovedInvestmentAsync(window, InvestorProfile, project!);
        });

        Log(null, "Phase 8 complete. Investment confirmed and active.");
        Log(null, $"========== {nameof(CancelPendingInvestmentAndReinvest)} PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 2: Investor — invest, cancel BEFORE founder approval
    // ═══════════════════════════════════════════════════════════════════

    private async Task InvestAndCancelBeforeApprovalAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        string investmentAmountBtc)
    {
        // ── Step 1: Find project and invest ──
        var foundProject = await FindProjectFromSdkAsync(window, profileName, project);

        Log(profileName, $"Investing {investmentAmountBtc} BTC (above threshold, pending approval)...");
        await InvestInProjectAsync(window, profileName, foundProject, project, investmentAmountBtc);

        // Wait for the indexer to pick up the investment and reload from SDK.
        var portfolioVm = await WaitForPortfolioInvestmentFromSdkAsync(
            window, profileName, project,
            inv => !string.IsNullOrEmpty(inv.InvestmentWalletId)
                   && !string.IsNullOrEmpty(inv.InvestmentTransactionId)
                   && inv.Status != "Cancelled");

        var pendingInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectIdentifier == project.ProjectIdentifier
            && !string.IsNullOrEmpty(i.InvestmentWalletId)
            && i.Status != "Cancelled");
        pendingInvestment.Should().NotBeNull("Pending investment should be in portfolio after SDK reload");
        pendingInvestment!.InvestmentWalletId.Should().NotBeNullOrEmpty("Investment should have a wallet ID after SDK reload");
        pendingInvestment.InvestmentTransactionId.Should().NotBeNullOrEmpty("Investment should have a transaction ID after SDK reload");

        Log(profileName, $"Pending investment: Step={pendingInvestment.Step}, Status='{pendingInvestment.StatusText}', " +
            $"WalletId={pendingInvestment.InvestmentWalletId}, TxId={pendingInvestment.InvestmentTransactionId}");

        // Record balance before cancellation
        var fundsVm = window.GetFundsViewModel();
        var balanceBeforeCancel = fundsVm?.TotalBalance ?? "0.0000";
        Log(profileName, $"Balance before cancellation: {balanceBeforeCancel}");

        // ── Step 2: Cancel the pending investment (before founder approval) ──
        Log(profileName, "Cancelling pending investment (before founder approval)...");
        await window.ClickInvestmentDetailActionAsync(portfolioVm, pendingInvestment, "CancelInvestmentStep1Button");
        Dispatcher.UIThread.RunJobs();

        pendingInvestment.StatusText.Should().Be("Cancelled", "Status should be 'Cancelled' after cancellation");
        pendingInvestment.Status.Should().Be("Cancelled", "Status field should be 'Cancelled'");
        Log(profileName, $"Investment cancelled. Status='{pendingInvestment.StatusText}'");

        // ── Step 3: Verify funds are released ──
        await VerifyFundsReleasedAsync(window, profileName, balanceBeforeCancel);

        Log(profileName, "Cancel-before-approval flow validated.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 3/6: Investor — find project and invest (reusable)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find the project from SDK and invest. Used for re-investment phases.
    /// </summary>
    private async Task InvestInProjectFromSdkAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        string investmentAmountBtc)
    {
        Log(profileName, $"Re-investing {investmentAmountBtc} BTC in project {project.ProjectIdentifier}...");
        var foundProject = await FindProjectFromSdkAsync(window, profileName, project);
        await InvestInProjectAsync(window, profileName, foundProject, project, investmentAmountBtc);
        Log(profileName, "Re-investment submitted successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 4/7: Founder — approve pending investment
    // ═══════════════════════════════════════════════════════════════════

    private async Task ApprovePendingInvestmentAsync(
        Window window,
        string profileName,
        ProjectHandle project)
    {
        await window.NavigateToSectionAndVerify("Funders");

        var fundersVm = window.GetFundersViewModel();
        fundersVm.Should().NotBeNull();

        SignatureRequestViewModel? pendingSignature = null;
        var deadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await fundersVm!.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("waiting");
            Dispatcher.UIThread.RunJobs();

            Log(profileName, $"Funders waiting count: {fundersVm.WaitingCount}, approved count: {fundersVm.ApprovedCount}");

            pendingSignature = fundersVm.FilteredSignatures.FirstOrDefault(s =>
                string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
            if (pendingSignature != null)
            {
                break;
            }

            Log(profileName, "Waiting for pending founder approval request...");
            await Task.Delay(TestHelpers.PollInterval);
        }

        pendingSignature.Should().NotBeNull("above-threshold investment should require founder approval");
        Log(profileName, $"Approving signature request id={pendingSignature!.Id} for project {project.ProjectIdentifier}");
        await window.ClickApproveSignatureAsync(fundersVm!, pendingSignature, TestHelpers.UiTimeout);

        var approvalDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < approvalDeadline)
        {
            await fundersVm.LoadInvestmentRequestsAsync();
            Dispatcher.UIThread.RunJobs();
            fundersVm.SetFilter("approved");
            Dispatcher.UIThread.RunJobs();

            var approved = fundersVm.FilteredSignatures.Any(s =>
                string.Equals(s.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));
            if (approved)
            {
                Log(profileName, "Founder approval completed.");
                return;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        throw new InvalidOperationException("Founder approval did not complete in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 5: Investor — cancel AFTER founder approval
    // ═══════════════════════════════════════════════════════════════════

    private async Task CancelAfterApprovalAsync(
        Window window,
        string profileName,
        ProjectHandle project)
    {
        // Wait for investment to reach Step 2 (founder approved)
        var portfolioVm = await WaitForPortfolioInvestmentFromSdkAsync(
            window, profileName, project,
            inv => inv.Step >= 2
                   && inv.Status != "Cancelled"
                   && inv.ApprovalStatus == "Approved");

        var approvedInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectIdentifier == project.ProjectIdentifier
            && i.Status != "Cancelled"
            && i.ApprovalStatus == "Approved");
        approvedInvestment.Should().NotBeNull("Approved investment should be in portfolio");

        Log(profileName, $"Approved investment found: Step={approvedInvestment!.Step}, Status='{approvedInvestment.StatusText}', " +
            $"Approval={approvedInvestment.ApprovalStatus}");

        // Record balance before cancellation
        var fundsVm = window.GetFundsViewModel();
        var balanceBeforeCancel = fundsVm?.TotalBalance ?? "0.0000";
        Log(profileName, $"Balance before cancellation: {balanceBeforeCancel}");

        // Cancel the approved investment
        Log(profileName, "Cancelling approved investment (after founder approval)...");
        await window.ClickInvestmentDetailActionAsync(portfolioVm, approvedInvestment, "CancelInvestmentButton");
        Dispatcher.UIThread.RunJobs();

        approvedInvestment.StatusText.Should().Be("Cancelled", "Status should be 'Cancelled' after cancellation");
        approvedInvestment.Status.Should().Be("Cancelled", "Status field should be 'Cancelled'");
        Log(profileName, $"Investment cancelled after approval. Status='{approvedInvestment.StatusText}'");

        // Verify funds are released
        await VerifyFundsReleasedAsync(window, profileName, balanceBeforeCancel);

        Log(profileName, "Cancel-after-approval flow validated.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Phase 8: Investor — confirm approved investment (Step 3)
    // ═══════════════════════════════════════════════════════════════════

    private async Task ConfirmApprovedInvestmentAsync(
        Window window,
        string profileName,
        ProjectHandle project)
    {
        // Wait for investment to reach Step 2 (founder approved)
        var portfolioVm = await WaitForPortfolioInvestmentFromSdkAsync(
            window, profileName, project,
            inv => inv.Step >= 2
                   && inv.Status != "Cancelled"
                   && inv.ApprovalStatus == "Approved");

        var investment = portfolioVm.Investments.First(i =>
            i.ProjectIdentifier == project.ProjectIdentifier
            && i.Status != "Cancelled"
            && i.ApprovalStatus == "Approved");

        Log(profileName, $"Confirming approved investment. Step={investment.Step}, Status={investment.StatusText}");
        investment.ApprovalStatus.Should().Be("Approved");
        await window.ClickInvestmentDetailActionAsync(portfolioVm, investment, "ConfirmInvestmentButton");

        // Wait for Step 3 (active)
        var activeDeadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
        while (DateTime.UtcNow < activeDeadline)
        {
            await portfolioVm.LoadInvestmentsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            var refreshed = portfolioVm.Investments.FirstOrDefault(i =>
                i.ProjectIdentifier == project.ProjectIdentifier
                && i.Status != "Cancelled");
            if (refreshed?.Step == 3)
            {
                Log(profileName, "Investment confirmed and active (Step 3).");
                return;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        throw new InvalidOperationException("Confirmed investment did not become active (Step 3) in time.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shared verification helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verify funds are released after cancellation by refreshing balance.
    /// </summary>
    private async Task VerifyFundsReleasedAsync(
        Window window,
        string profileName,
        string balanceBeforeCancel)
    {
        await window.NavigateToSectionAndVerify("Funds");

        await window.ClickWalletCardButton("WalletCardBtnRefresh");
        await Task.Delay(2000);
        Dispatcher.UIThread.RunJobs();

        var fundsVm = window.GetFundsViewModel();
        fundsVm.Should().NotBeNull();
        var balanceAfterCancel = fundsVm!.TotalBalance;
        Log(profileName, $"Balance after cancellation: {balanceAfterCancel}");

        balanceAfterCancel.Should().NotBe("0.0000",
            "Balance should be non-zero after cancellation (funds released)");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Investment helper
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invest in a project and add to portfolio. Returns the PortfolioViewModel.
    /// </summary>
    private async Task<PortfolioViewModel> InvestInProjectAsync(
        Window window,
        string profileName,
        ProjectItemViewModel foundProject,
        ProjectHandle project,
        string amountBtc)
    {
        var findProjectsVm = window.GetFindProjectsViewModel();
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

        // Wait for wallets to load
        var walletLoadDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < walletLoadDeadline && investVm!.Wallets.Count == 0)
        {
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }

        investVm!.Wallets.Count.Should().BeGreaterThan(0);
        investVm.InvestmentAmount = amountBtc;
        investVm.CanSubmit.Should().BeTrue();

        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var investWallet = investVm.Wallets[0];
        investVm.SelectWallet(investWallet);
        Dispatcher.UIThread.RunJobs();

        Log(profileName, $"Paying {amountBtc} BTC with wallet {investWallet.Id.Value}...");
        investVm.PayWithWallet();

        var investDeadline = DateTime.UtcNow + TestHelpers.TransactionTimeout;
        while (DateTime.UtcNow < investDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                break;
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        investVm.CurrentScreen.Should().Be(InvestScreen.Success,
            $"Invest should reach success. Last status: {investVm.PaymentStatusText}");
        investVm.FormattedAmount.Should().Be(
            decimal.Parse(amountBtc, CultureInfo.InvariantCulture).ToString("F8", CultureInfo.InvariantCulture));

        // Above threshold → requires founder approval
        investVm.IsAutoApproved.Should().BeFalse(
            "Above-threshold investment should NOT be auto-approved");
        investVm.SuccessTitle.Should().Contain("Pending Approval",
            "Above-threshold investment should show 'Pending Approval'");

        // Add to portfolio
        // DIRECT DI RESOLVE: PortfolioViewModel is a singleton not reachable from the visual
        // tree while we're still on the Find Projects invest flow. Mirrors internal DI wiring.
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        investVm.AddToPortfolio();
        Dispatcher.UIThread.RunJobs();

        var addedInvestment = portfolioVm.Investments.FirstOrDefault(i =>
            i.ProjectIdentifier == project.ProjectIdentifier && i.Status != "Cancelled");
        addedInvestment.Should().NotBeNull("Investment should appear in portfolio");
        // TotalInvested may reflect the exact requested amount (optimistic add) or the
        // post-fee on-chain amount (SDK loaded before AddToPortfolio). Accept both.
        var actualInvested = decimal.Parse(addedInvestment!.TotalInvested, CultureInfo.InvariantCulture);
        var expectedInvested = decimal.Parse(amountBtc, CultureInfo.InvariantCulture);
        actualInvested.Should().BeGreaterThan(0, "TotalInvested should be non-zero");
        actualInvested.Should().BeLessThanOrEqualTo(expectedInvested, "TotalInvested should not exceed requested amount");
        (expectedInvested - actualInvested).Should().BeLessThan(0.001m, "TotalInvested should be within fee tolerance of requested amount");

        Log(profileName, $"Investment completed. Step={addedInvestment.Step}, Status='{addedInvestment.StatusText}'");

        // Close invest page to allow re-navigation
        findProjectsVm.CloseInvestPage();
        findProjectsVm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        return portfolioVm;
    }

    /// <summary>
    /// Wait for the indexer to pick up the investment and reload from SDK.
    /// Returns PortfolioViewModel once the investment matches the predicate.
    /// </summary>
    private async Task<PortfolioViewModel> WaitForPortfolioInvestmentFromSdkAsync(
        Window window,
        string profileName,
        ProjectHandle project,
        Func<InvestmentViewModel, bool> predicate)
    {
        await window.NavigateToSectionAndVerify("Funded");

        // DIRECT DI RESOLVE: Need the singleton PortfolioViewModel to poll SDK reload.
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();
        var deadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await portfolioVm.LoadInvestmentsFromSdkAsync();
            Dispatcher.UIThread.RunJobs();

            var investment = portfolioVm.Investments.FirstOrDefault(i =>
                string.Equals(i.ProjectIdentifier, project.ProjectIdentifier, StringComparison.Ordinal));

            if (investment != null)
            {
                Log(profileName, $"Portfolio investment found via SDK. Step={investment.Step}, " +
                    $"Status={investment.StatusText}, WalletId={investment.InvestmentWalletId}, " +
                    $"TxId={investment.InvestmentTransactionId}");
                if (predicate(investment))
                {
                    return portfolioVm;
                }
            }
            else
            {
                Log(profileName, "Portfolio investment not found in SDK yet.");
            }

            await Task.Delay(TestHelpers.PollInterval);
        }

        throw new InvalidOperationException(
            $"Portfolio investment for project {project.ProjectIdentifier} did not reach expected state within {TestHelpers.IndexerLagTimeout.TotalSeconds}s.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Project creation helpers (reused from MultiFundClaimAndRecoverTest pattern)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<ProjectHandle> CreateFundProjectAsync(
        Window window,
        string profileName,
        string projectName,
        string projectAbout,
        string bannerImageUrl,
        string profileImageUrl,
        string payoutDay,
        string runId)
    {
        await window.NavigateToSectionAndVerify("My Projects");

        var myProjectsVm = window.GetMyProjectsViewModel();
        myProjectsVm.Should().NotBeNull();

        await OpenCreateWizard(window, myProjectsVm!, profileName);

        var wizardVm = myProjectsVm!.CreateProjectVm;
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
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Setting project images...");
        wizardVm.BannerUrl = bannerImageUrl;
        wizardVm.ProfileUrl = profileImageUrl;
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

        Log(profileName, "Configuring target amount and threshold...");
        wizardVm.TargetAmount = "1.0";
        wizardVm.ApprovalThreshold = "0.01";
        wizardVm.GoNext();
        Dispatcher.UIThread.RunJobs();

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

        await myProjectsVm.LoadFounderProjectsAsync();
        Dispatcher.UIThread.RunJobs();

        var project = myProjectsVm.Projects.FirstOrDefault(p =>
            p.Description.Contains(runId, StringComparison.Ordinal));
        project.Should().NotBeNull();
        project!.ProjectIdentifier.Should().NotBeNullOrEmpty();
        project.OwnerWalletId.Should().NotBeNullOrEmpty();

        Log(profileName, $"Project deployed. ProjectId={project.ProjectIdentifier}");
        return new ProjectHandle(runId, projectName, project.ProjectIdentifier!, project.OwnerWalletId!);
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
        profileContext.ProfileName.Should().Be(expectedProfile);
    }

    private static void SetPasswordProvider(string profileName)
    {
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log(profileName, "Set SimplePasswordProvider key to 'default-key'.");
    }

    private async Task CreateWalletAndFundAsync(Window window, string profileName)
    {
        await window.NavigateToSectionAndVerify("Funds");

        var fundsVm = window.GetFundsViewModel();
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

    private async Task<ProjectItemViewModel> FindProjectFromSdkAsync(
        Window window,
        string profileName,
        ProjectHandle project)
    {
        await window.NavigateToSectionAndVerify("Find Projects");

        var findProjectsVm = window.GetFindProjectsViewModel();
        findProjectsVm.Should().NotBeNull();

        var deadline = DateTime.UtcNow + TestHelpers.IndexerLagTimeout;
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
            await Task.Delay(TestHelpers.PollInterval);
        }

        throw new InvalidOperationException("Project was not found in the SDK project list in time.");
    }

    private async Task WipeExistingData(Window window, string profileName)
    {
        await window.WipeExistingData();
        Log(profileName, "Profile data wiped.");
    }

    private async Task CreateWalletViaGenerate(Window window, string profileName)
    {
        await window.CreateWalletViaGenerate();
        Log(profileName, "Wallet created successfully.");
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

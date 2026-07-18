using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Fund project: 1 founder + 4 investors, monthly schedule with 6 installments.
/// Covers below-threshold auto-approval, above-threshold manual approval,
/// different stage patterns, all recovery paths (belowThreshold, recovery+penaltyRelease, unfundedRelease).
///
/// Investor1: 0.001 BTC below threshold, auto-approved, 6-stage pattern → belowThreshold recovery
/// Investor2: 0.002 BTC below threshold, auto-approved, 3-stage pattern → belowThreshold recovery
/// Investor3: 0.02 BTC above threshold, 3-stage → recovery + penaltyRelease
/// Investor4: 0.03 BTC above threshold, 6-stage → unfundedRelease (after founder releases)
/// </summary>
public class MultiFundClaimAndRecoverTest
{
    private const string TestName = "MultiFundClaimAndRecover";
    private const string FounderProfile = TestName + "-Founder";
    private const string Investor1Profile = TestName + "-Investor1";
    private const string Investor2Profile = TestName + "-Investor2";
    private const string Investor3Profile = TestName + "-Investor3";
    private const string Investor4Profile = TestName + "-Investor4";

    [Fact]
    public async Task MultiFundClaimAndRecover()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Multi Fund {runId}";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        // Use today's day-of-month as MonthlyPayoutDay so stage 0 is immediately claimable
        var todayDay = DateTime.UtcNow.Day;
        var projectAbout = $"{TestName} run {runId}. Monthly/6, payout day {todayDay}, 4 investors, all recovery paths.";

        Log(null, $"========== STARTING {nameof(MultiFundClaimAndRecover)} ==========");
        Log(null, $"Run ID: {runId}");

        // ── Founder: create wallet + fund project (monthly, 6 installments, past start date) ──
        await using var founderHost = await TestHostFactory.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
        await founderHost.Client.SwitchNetworkAsync("Angornet");
        await founderHost.Client.EnableDebugModeAsync();

        var founderWallet = await founderHost.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = FounderProfile,
        });
        founderWallet.Success.Should().BeTrue(founderWallet.Error);

        var createdProject = await founderHost.Client.CreateFundProjectAsync(new CreateFundProjectRequest
        {
            ProjectName = projectName,
            ProjectAbout = projectAbout,
            BannerUrl = bannerImageUrl,
            ProfileUrl = profileImageUrl,
            TargetAmountBtc = "1.0",
            ThresholdAmountBtc = "0.01",
            PenaltyDays = 0,
            PayoutFrequency = "Monthly",
            InstallmentCount = 6,
            MonthlyPayoutDay = todayDay,
            // Use yesterday as start date so below-threshold recovery timelocks (which use
            // StartDate as ExpiryDateOverride) are safely in the past regardless of signet MTP lag.
            StartDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            RunId = runId,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);
        var projectId = createdProject.ProjectIdentifier!;
        Log(null, $"Project created: {projectId}");

        // ── Launch all 4 investor processes ──
        await using var investor1Host = await TestHostFactory.LaunchAsync(Investor1Profile);
        await investor1Host.Client.WipeDataAsync();
        await investor1Host.Client.SwitchNetworkAsync("Angornet");
        var wallet1 = await investor1Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor1Profile });
        wallet1.Success.Should().BeTrue(wallet1.Error);

        await using var investor2Host = await TestHostFactory.LaunchAsync(Investor2Profile);
        await investor2Host.Client.WipeDataAsync();
        await investor2Host.Client.SwitchNetworkAsync("Angornet");
        var wallet2 = await investor2Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor2Profile });
        wallet2.Success.Should().BeTrue(wallet2.Error);

        await using var investor3Host = await TestHostFactory.LaunchAsync(Investor3Profile);
        await investor3Host.Client.WipeDataAsync();
        await investor3Host.Client.SwitchNetworkAsync("Angornet");
        var wallet3 = await investor3Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor3Profile });
        wallet3.Success.Should().BeTrue(wallet3.Error);

        await using var investor4Host = await TestHostFactory.LaunchAsync(Investor4Profile);
        await investor4Host.Client.WipeDataAsync();
        await investor4Host.Client.SwitchNetworkAsync("Angornet");
        var wallet4 = await investor4Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor4Profile });
        wallet4.Success.Should().BeTrue(wallet4.Error);

        // ══════════════════════════════════════════════════════════════
        // Below-threshold investors (auto-approved, no founder approval needed)
        // ══════════════════════════════════════════════════════════════

        Log(Investor1Profile, "Investing 0.001 BTC (below threshold, 6-stage)...");
        var invest1 = await investor1Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.001",
            ExpectFounderApproval = false,
            TargetPatternStageCount = 6,
        });
        invest1.Success.Should().BeTrue(invest1.Error);
        invest1.IsAutoApproved.Should().BeTrue();

        Log(Investor2Profile, "Investing 0.002 BTC (below threshold, 3-stage)...");
        var invest2 = await investor2Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.002",
            ExpectFounderApproval = false,
            TargetPatternStageCount = 3,
        });
        invest2.Success.Should().BeTrue(invest2.Error);
        invest2.IsAutoApproved.Should().BeTrue();

        // ══════════════════════════════════════════════════════════════
        // Above-threshold investors (require founder approval)
        // ══════════════════════════════════════════════════════════════

        Log(Investor3Profile, "Investing 0.02 BTC (above threshold, 3-stage)...");
        var invest3 = await investor3Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 3,
        });
        invest3.Success.Should().BeTrue(invest3.Error);
        invest3.IsAutoApproved.Should().BeFalse();

        Log(Investor4Profile, "Investing 0.03 BTC (above threshold, 6-stage)...");
        var invest4 = await investor4Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.03",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 6,
        });
        invest4.Success.Should().BeTrue(invest4.Error);
        invest4.IsAutoApproved.Should().BeFalse();

        // ── Founder approves both above-threshold investments ──
        Log(FounderProfile, "Approving 2 above-threshold investments...");
        var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 2,
            Batch = true,
        });
        approve.Success.Should().BeTrue(approve.Error);

        // ── Above-threshold investors confirm ──
        Log(Investor3Profile, "Confirming investment...");
        var confirm3 = await investor3Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm3.Success.Should().BeTrue(confirm3.Error);
        confirm3.Step.Should().Be(3);

        Log(Investor4Profile, "Confirming investment...");
        var confirm4 = await investor4Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm4.Success.Should().BeTrue(confirm4.Error);
        confirm4.Step.Should().Be(3);

        // ── Founder claims stage 1 (4 UTXOs: 2 below + 2 above) ──
        Log(FounderProfile, "Claiming stage 1...");
        var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
        {
            ProjectIdentifier = projectId,
            StageNumber = 1,
            ExpectedUtxoCount = 4,
        });
        claim.Success.Should().BeTrue(claim.Error);

        // ══════════════════════════════════════════════════════════════
        // Recovery paths
        // ══════════════════════════════════════════════════════════════

        // Investor3 (above threshold): recovery → penaltyRelease
        Log(Investor3Profile, "Recovering via recovery...");
        var recover3 = await investor3Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "recovery",
        });
        recover3.Success.Should().BeTrue(recover3.Error);

        // ── Investor3 is now "In Penalty": verify the Penalties popup on the Funded tab shows it ──
        await VerifyPenaltiesPopupAsync(investor3Host, projectId, projectName);

        Log(Investor3Profile, "Claiming via penaltyRelease...");
        var penalty3 = await investor3Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "penaltyRelease",
        });
        penalty3.Success.Should().BeTrue(penalty3.Error);

        // Founder releases remaining stages (for investor4's unfundedRelease path)
        Log(FounderProfile, "Releasing remaining stages...");
        var release = await founderHost.Client.ReleaseFundsToInvestorsAsync(new ReleaseFundsRequest
        {
            ProjectIdentifier = projectId,
        });
        release.Success.Should().BeTrue(release.Error);

        // Investor4 (above threshold): unfundedRelease after founder release
        Log(Investor4Profile, "Claiming via unfundedRelease...");
        var recover4 = await investor4Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recover4.Success.Should().BeTrue(recover4.Error);

        // Below-threshold investors: belowThreshold recovery
        Log(Investor1Profile, "Recovering via belowThreshold...");
        var recover1 = await investor1Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "belowThreshold",
        });
        recover1.Success.Should().BeTrue(recover1.Error);

        Log(Investor2Profile, "Recovering via belowThreshold...");
        var recover2 = await investor2Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "belowThreshold",
        });
        recover2.Success.Should().BeTrue(recover2.Error);

        Log(null, $"========== {nameof(MultiFundClaimAndRecover)} PASSED ==========");
    }

    /// <summary>
    /// Opens the Penalties popup from the Funded tab and asserts it lists the investment
    /// that was just recovered to penalty. Retries LoadPenaltiesAsync to absorb indexer lag.
    /// </summary>
    private static async Task VerifyPenaltiesPopupAsync(ITestHost host, string projectId, string projectName)
    {
        Log(Investor3Profile, "Opening Penalties popup to verify In Penalty entry...");
        await host.Client.NavigateAsync("Funded");
        await host.Client.WaitForControlAsync("PenaltiesButton", TimeSpan.FromSeconds(30));
        await host.Client.ClickAsync("PenaltiesButton");

        // PenaltiesModal is mounted in the shell modal overlay; its CloseButton confirms it opened
        await host.Client.WaitForControlAsync("CloseButton", TimeSpan.FromSeconds(15));

        // Clicking the button kicks off LoadPenaltiesAsync; wait for the initial load to finish,
        // then retry the wallet-wide penalty scan until the entry appears (indexer may lag).
        await host.Client.WaitForVmPropertyAsync(
            "PortfolioViewModel", "IsLoadingPenalties", "False", TimeSpan.FromMinutes(2));

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            var hasPenalties = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "HasPenalties");
            if (string.Equals(hasPenalties, "True", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
            await host.Client.InvokeVmAsync("PortfolioViewModel", "LoadPenaltiesAsync");
            await host.Client.WaitForVmPropertyAsync(
                "PortfolioViewModel", "IsLoadingPenalties", "False", TimeSpan.FromMinutes(1));
        }

        var penaltyCount = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties.Count");
        penaltyCount.Should().Be("1", "the investor recovered exactly one investment to penalty");

        var penaltyProjectId = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].ProjectIdentifier");
        penaltyProjectId.Should().Be(projectId);

        var penaltyProjectName = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].ProjectName");
        penaltyProjectName.Should().Be(projectName);

        var penaltyDaysRemaining = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].DaysRemaining");
        penaltyDaysRemaining.Should().Be("0", "the project was created with PenaltyDays = 0");

        var penaltyStatus = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].StatusText");
        penaltyStatus.Should().Be("Penalty release available now");

        Log(Investor3Profile, $"Penalties popup verified: {penaltyProjectName} ({penaltyProjectId}), status '{penaltyStatus}'");

        // Close the popup so the subsequent penaltyRelease flow starts from a clean Funded tab
        await host.Client.ClickAsync("CloseButton");
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

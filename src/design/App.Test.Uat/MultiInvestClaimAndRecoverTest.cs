using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Invest project: 1 founder + 4 investors.
/// Covers cancel-before-approval, cancel-after-approval with reinvest,
/// founder claim stage 1, founder release remaining, all investors unfundedRelease.
///
/// Investor1: invest → cancel before approval (Step 1) → reinvest
/// Investor2: invest → founder approves just this one → cancel after approval (Step 2) → reinvest
/// Investor3: invest normally (0.02 BTC)
/// Investor4: invest normally (0.03 BTC)
/// Founder approves all 4 remaining investments, all confirm.
/// Founder claims stage 1, releases remaining stages.
/// All 4 investors claim via unfundedRelease.
/// </summary>
public class MultiInvestClaimAndRecoverTest
{
    private const string TestName = "MultiInvestClaimAndRecover";
    private const string FounderProfile = TestName + "-Founder";
    private const string Investor1Profile = TestName + "-Investor1";
    private const string Investor2Profile = TestName + "-Investor2";
    private const string Investor3Profile = TestName + "-Investor3";
    private const string Investor4Profile = TestName + "-Investor4";

    [Fact]
    public async Task MultiInvestClaimAndRecover()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Multi Invest {runId}";
        var projectAbout = $"{TestName} run {runId}. Cancel/reinvest + claim + release.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        Log(null, $"========== STARTING {nameof(MultiInvestClaimAndRecover)} ==========");
        Log(null, $"Run ID: {runId}");

        // ── Founder: create wallet + invest project ──
        await using var founderHost = await TestProcessHost.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
        await founderHost.Client.SwitchNetworkAsync("Angornet");

        var founderWallet = await founderHost.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = FounderProfile,
        });
        founderWallet.Success.Should().BeTrue(founderWallet.Error);

        var createdProject = await founderHost.Client.CreateInvestProjectAsync(new CreateInvestProjectRequest
        {
            ProjectName = projectName,
            ProjectAbout = projectAbout,
            BannerUrl = bannerImageUrl,
            ProfileUrl = profileImageUrl,
            RunId = runId,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);
        var projectId = createdProject.ProjectIdentifier!;
        Log(null, $"Project created: {projectId}");

        // ── Launch all 4 investor processes ──
        await using var investor1Host = await TestProcessHost.LaunchAsync(Investor1Profile);
        await investor1Host.Client.WipeDataAsync();
        await investor1Host.Client.SwitchNetworkAsync("Angornet");
        var wallet1 = await investor1Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor1Profile });
        wallet1.Success.Should().BeTrue(wallet1.Error);

        await using var investor2Host = await TestProcessHost.LaunchAsync(Investor2Profile);
        await investor2Host.Client.WipeDataAsync();
        await investor2Host.Client.SwitchNetworkAsync("Angornet");
        var wallet2 = await investor2Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor2Profile });
        wallet2.Success.Should().BeTrue(wallet2.Error);

        await using var investor3Host = await TestProcessHost.LaunchAsync(Investor3Profile);
        await investor3Host.Client.WipeDataAsync();
        await investor3Host.Client.SwitchNetworkAsync("Angornet");
        var wallet3 = await investor3Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor3Profile });
        wallet3.Success.Should().BeTrue(wallet3.Error);

        await using var investor4Host = await TestProcessHost.LaunchAsync(Investor4Profile);
        await investor4Host.Client.WipeDataAsync();
        await investor4Host.Client.SwitchNetworkAsync("Angornet");
        var wallet4 = await investor4Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = Investor4Profile });
        wallet4.Success.Should().BeTrue(wallet4.Error);

        // ══════════════════════════════════════════════════════════════
        // Investor1: invest → cancel before approval (Step 1) → reinvest
        // ══════════════════════════════════════════════════════════════
        Log(Investor1Profile, "Investing (first attempt, will cancel before approval)...");
        var invest1a = await investor1Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = false, // Don't wait for approval — we cancel first
            TargetPatternStageCount = 0,
        });
        invest1a.Success.Should().BeTrue(invest1a.Error);

        Log(Investor1Profile, "Cancelling investment before approval (Step 1)...");
        var cancel1 = await investor1Host.Client.CancelInvestmentAsync(new CancelInvestmentRequest
        {
            ProjectIdentifier = projectId,
            CancelStage = "beforeApproval",
        });
        cancel1.Success.Should().BeTrue(cancel1.Error);

        Log(Investor1Profile, "Reinvesting after cancel...");
        var invest1b = await investor1Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest1b.Success.Should().BeTrue(invest1b.Error);

        // ══════════════════════════════════════════════════════════════
        // Investor2: invest → founder approves → cancel after approval (Step 2) → reinvest
        // ══════════════════════════════════════════════════════════════
        Log(Investor2Profile, "Investing (first attempt, will cancel after approval)...");
        var invest2a = await investor2Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest2a.Success.Should().BeTrue(invest2a.Error);

        // Founder approves just investor2's first investment (expectedCount=1 since investor1's was cancelled)
        Log(FounderProfile, "Approving investor2's first investment...");
        var approveForCancel = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 1,
            Batch = true,
        });
        approveForCancel.Success.Should().BeTrue(approveForCancel.Error);

        Log(Investor2Profile, "Cancelling investment after approval (Step 2)...");
        var cancel2 = await investor2Host.Client.CancelInvestmentAsync(new CancelInvestmentRequest
        {
            ProjectIdentifier = projectId,
            CancelStage = "afterApproval",
        });
        cancel2.Success.Should().BeTrue(cancel2.Error);

        Log(Investor2Profile, "Reinvesting after cancel...");
        var invest2b = await investor2Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest2b.Success.Should().BeTrue(invest2b.Error);

        // ══════════════════════════════════════════════════════════════
        // Investor3 + Investor4: invest normally
        // ══════════════════════════════════════════════════════════════
        Log(Investor3Profile, "Investing 0.02 BTC...");
        var invest3 = await investor3Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest3.Success.Should().BeTrue(invest3.Error);

        Log(Investor4Profile, "Investing 0.03 BTC...");
        var invest4 = await investor4Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.03",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest4.Success.Should().BeTrue(invest4.Error);

        // ── Founder approves all 4 remaining investments ──
        Log(FounderProfile, "Approving all 4 investments...");
        var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 4,
            Batch = true,
        });
        approve.Success.Should().BeTrue(approve.Error);

        // ── All 4 investors confirm ──
        Log(Investor1Profile, "Confirming investment...");
        var confirm1 = await investor1Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm1.Success.Should().BeTrue(confirm1.Error);
        confirm1.Step.Should().Be(3);

        Log(Investor2Profile, "Confirming investment...");
        var confirm2 = await investor2Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm2.Success.Should().BeTrue(confirm2.Error);
        confirm2.Step.Should().Be(3);

        Log(Investor3Profile, "Confirming investment...");
        var confirm3 = await investor3Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm3.Success.Should().BeTrue(confirm3.Error);
        confirm3.Step.Should().Be(3);

        Log(Investor4Profile, "Confirming investment...");
        var confirm4 = await investor4Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest { ProjectIdentifier = projectId });
        confirm4.Success.Should().BeTrue(confirm4.Error);
        confirm4.Step.Should().Be(3);

        // ── Founder claims stage 1 (4 UTXOs) ──
        Log(FounderProfile, "Claiming stage 1...");
        var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
        {
            ProjectIdentifier = projectId,
            StageNumber = 1,
            ExpectedUtxoCount = 4,
        });
        claim.Success.Should().BeTrue(claim.Error);

        // ── Founder releases remaining stages ──
        Log(FounderProfile, "Releasing remaining stages...");
        var release = await founderHost.Client.ReleaseFundsToInvestorsAsync(new ReleaseFundsRequest
        {
            ProjectIdentifier = projectId,
        });
        release.Success.Should().BeTrue(release.Error);

        // ── All 4 investors claim via unfundedRelease ──
        Log(Investor1Profile, "Recovering via unfundedRelease...");
        var recovery1 = await investor1Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery1.Success.Should().BeTrue(recovery1.Error);

        Log(Investor2Profile, "Recovering via unfundedRelease...");
        var recovery2 = await investor2Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery2.Success.Should().BeTrue(recovery2.Error);

        Log(Investor3Profile, "Recovering via unfundedRelease...");
        var recovery3 = await investor3Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery3.Success.Should().BeTrue(recovery3.Error);

        Log(Investor4Profile, "Recovering via unfundedRelease...");
        var recovery4 = await investor4Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery4.Success.Should().BeTrue(recovery4.Error);

        Log(null, $"========== {nameof(MultiInvestClaimAndRecover)} PASSED ==========");
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

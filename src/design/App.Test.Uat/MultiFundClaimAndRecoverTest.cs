using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Per-process UAT version of the integration MultiFundClaimAndRecoverTest.
/// Fund project: 1 founder + 2 investors (below/above threshold).
/// Founder claims stage 1, above-threshold investor recovers to penalty then from penalty,
/// below-threshold investor recovers via end-of-project path.
/// </summary>
public class MultiFundClaimAndRecoverTest
{
    private const string TestName = "MultiFundClaimAndRecover";
    private const string FounderProfile = TestName + "-Founder";
    private const string BelowThresholdInvestorProfile = TestName + "-Investor1";
    private const string AboveThresholdInvestorProfile = TestName + "-Investor2";

    [Fact]
    public async Task MultiFundClaimAndRecover()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Multi Fund {runId}";
        var projectAbout = $"{TestName} run {runId}. Founder claims stage 1, one investor recovers with penalty then from penalty, the other without penalty.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var payoutDay = DateTime.UtcNow.DayOfWeek.ToString();

        Log(null, $"========== STARTING {nameof(MultiFundClaimAndRecover)} ==========");
        Log(null, $"Run ID: {runId}");

        await using var founderHost = await TestProcessHost.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
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
            ThresholdAmountBtc = "0.01",
            PayoutDay = payoutDay,
            RunId = runId,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);
        var projectId = createdProject.ProjectIdentifier!;

        // ── Below-threshold investor (auto-approved) ──
        await using var investor1Host = await TestProcessHost.LaunchAsync(BelowThresholdInvestorProfile);
        await investor1Host.Client.WipeDataAsync();

        var wallet1 = await investor1Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = BelowThresholdInvestorProfile,
        });
        wallet1.Success.Should().BeTrue(wallet1.Error);

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

        // ── Above-threshold investor (requires approval) ──
        await using var investor2Host = await TestProcessHost.LaunchAsync(AboveThresholdInvestorProfile);
        await investor2Host.Client.WipeDataAsync();

        var wallet2 = await investor2Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = AboveThresholdInvestorProfile,
        });
        wallet2.Success.Should().BeTrue(wallet2.Error);

        var invest2 = await investor2Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 3,
        });
        invest2.Success.Should().BeTrue(invest2.Error);
        invest2.IsAutoApproved.Should().BeFalse();

        // ── Founder approves above-threshold investment ──
        var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 1,
            Batch = false,
        });
        approve.Success.Should().BeTrue(approve.Error);

        // ── Investor 2 confirms ──
        var confirm = await investor2Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
        {
            ProjectIdentifier = projectId,
        });
        confirm.Success.Should().BeTrue(confirm.Error);
        confirm.Step.Should().Be(3);

        // ── Founder claims stage 1 (2 UTXOs) ──
        var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
        {
            ProjectIdentifier = projectId,
            StageNumber = 1,
            ExpectedUtxoCount = 2,
        });
        claim.Success.Should().BeTrue(claim.Error);

        // ── Above-threshold investor: recover to penalty, then from penalty ──
        var recover = await investor2Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "recovery",
        });
        recover.Success.Should().BeTrue(recover.Error);

        var penaltyRelease = await investor2Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "penaltyRelease",
        });
        penaltyRelease.Success.Should().BeTrue(penaltyRelease.Error);

        // ── Below-threshold investor: end-of-project recovery ──
        var belowRecover = await investor1Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "belowThreshold",
        });
        belowRecover.Success.Should().BeTrue(belowRecover.Error);

        Log(null, $"========== {nameof(MultiFundClaimAndRecover)} PASSED ==========");
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Per-process UAT version of the integration MultiInvestClaimAndRecoverTest.
/// Invest project: 1 founder + 2 investors.
/// Founder claims stage 1, releases remaining stages, investors claim via unfunded-release path.
/// </summary>
public class MultiInvestClaimAndRecoverTest
{
    private const string TestName = "MultiInvestClaimAndRecover";
    private const string FounderProfile = TestName + "-Founder";
    private const string Investor1Profile = TestName + "-Investor1";
    private const string Investor2Profile = TestName + "-Investor2";

    [Fact]
    public async Task MultiInvestClaimAndRecover()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Multi Invest {runId}";
        var projectAbout = $"{TestName} run {runId}. Founder claims stage 1, releases rest, investors claim.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        Log(null, $"========== STARTING {nameof(MultiInvestClaimAndRecover)} ==========");
        Log(null, $"Run ID: {runId}");

        await using var founderHost = await TestProcessHost.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();

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

        // ── Investor 1 ──
        await using var investor1Host = await TestProcessHost.LaunchAsync(Investor1Profile);
        await investor1Host.Client.WipeDataAsync();

        var wallet1 = await investor1Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Investor1Profile,
        });
        wallet1.Success.Should().BeTrue(wallet1.Error);

        var invest1 = await investor1Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest1.Success.Should().BeTrue(invest1.Error);

        // ── Investor 2 ──
        await using var investor2Host = await TestProcessHost.LaunchAsync(Investor2Profile);
        await investor2Host.Client.WipeDataAsync();

        var wallet2 = await investor2Host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Investor2Profile,
        });
        wallet2.Success.Should().BeTrue(wallet2.Error);

        var invest2 = await investor2Host.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.03",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest2.Success.Should().BeTrue(invest2.Error);

        // ── Founder approves both investments ──
        var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 2,
            Batch = true,
        });
        approve.Success.Should().BeTrue(approve.Error);

        // ── Both investors confirm ──
        var confirm1 = await investor1Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
        {
            ProjectIdentifier = projectId,
        });
        confirm1.Success.Should().BeTrue(confirm1.Error);
        confirm1.Step.Should().Be(3);

        var confirm2 = await investor2Host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
        {
            ProjectIdentifier = projectId,
        });
        confirm2.Success.Should().BeTrue(confirm2.Error);
        confirm2.Step.Should().Be(3);

        // ── Founder claims stage 1 (2 UTXOs) ──
        var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
        {
            ProjectIdentifier = projectId,
            StageNumber = 1,
            ExpectedUtxoCount = 2,
        });
        claim.Success.Should().BeTrue(claim.Error);

        // ── Founder releases remaining stages ──
        var release = await founderHost.Client.ReleaseFundsToInvestorsAsync(new ReleaseFundsRequest
        {
            ProjectIdentifier = projectId,
        });
        release.Success.Should().BeTrue(release.Error);

        // ── Both investors claim via unfunded-release ──
        var recovery1 = await investor1Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery1.Success.Should().BeTrue(recovery1.Error);

        var recovery2 = await investor2Host.Client.ExecuteRecoveryAsync(new RecoveryRequest
        {
            ProjectIdentifier = projectId,
            Action = "unfundedRelease",
        });
        recovery2.Success.Should().BeTrue(recovery2.Error);

        Log(null, $"========== {nameof(MultiInvestClaimAndRecover)} PASSED ==========");
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

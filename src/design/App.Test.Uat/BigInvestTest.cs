using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

public class BigInvestTest
{
    private const string TestName = "BigInvest";
    private const string FounderProfile = TestName + "-Founder";
    private const int TotalInvestors = 15;

    private sealed record ProjectHandle(string RunId, string ProjectName, string ProjectIdentifier, string FounderWalletId);
    private sealed record InvestorConfig(string ProfileName, string AmountBtc);

    private static string InvestorProfile(int index) => $"{TestName}-Investor{index}";

    private static List<InvestorConfig> BuildInvestorConfigs()
    {
        var configs = new List<InvestorConfig>();
        var amounts = new[] { "0.02", "0.025", "0.03", "0.035", "0.04" };
        for (int i = 1; i <= TotalInvestors; i++)
        {
            configs.Add(new InvestorConfig(InvestorProfile(i), amounts[(i - 1) % amounts.Length]));
        }

        return configs;
    }

    [Fact]
    public async Task BigInvest()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Big Invest {runId}";
        var projectAbout = $"{TestName} run {runId}. 15-investor investment project: founder claims stage 1, releases rest, investors claim.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var investors = BuildInvestorConfigs();

        Log(null, $"========== STARTING {nameof(BigInvest)} ==========");
        Log(null, $"Run ID: {runId}");

        await using var founderHost = await TestHostFactory.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
        await founderHost.Client.EnableDebugModeAsync();

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

        var project = new ProjectHandle(runId, projectName, createdProject.ProjectIdentifier!, createdProject.OwnerWalletId!);

        var hosts = new Dictionary<string, ITestHost>(StringComparer.OrdinalIgnoreCase)
        {
            [FounderProfile] = founderHost,
        };

        try
        {
            foreach (var inv in investors)
            {
                var host = await GetOrCreateHostAsync(hosts, inv.ProfileName);

                var wallet = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
                {
                    ProfileName = inv.ProfileName,
                });
                wallet.Success.Should().BeTrue(wallet.Error);

                var invest = await host.Client.InvestInProjectAsync(new InvestInProjectRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                    RunId = runId,
                    ProjectName = project.ProjectName,
                    AmountBtc = inv.AmountBtc,
                    ExpectFounderApproval = true,
                    TargetPatternStageCount = 0,
                });
                invest.Success.Should().BeTrue(invest.Error);
                invest.IsAutoApproved.Should().BeFalse();
            }

            var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
            {
                ProjectIdentifier = project.ProjectIdentifier,
                ExpectedCount = TotalInvestors,
                Batch = true,
            });
            approve.Success.Should().BeTrue(approve.Error);

            foreach (var inv in investors)
            {
                var host = hosts[inv.ProfileName];
                var confirm = await host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                });
                confirm.Success.Should().BeTrue(confirm.Error);
                confirm.Step.Should().Be(3);
            }

            var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
            {
                ProjectIdentifier = project.ProjectIdentifier,
                StageNumber = 1,
                ExpectedUtxoCount = TotalInvestors,
            });
            claim.Success.Should().BeTrue(claim.Error);

            var release = await founderHost.Client.ReleaseFundsToInvestorsAsync(new ReleaseFundsRequest
            {
                ProjectIdentifier = project.ProjectIdentifier,
            });
            release.Success.Should().BeTrue(release.Error);

            foreach (var inv in investors)
            {
                var host = hosts[inv.ProfileName];
                var recovery = await host.Client.ExecuteRecoveryAsync(new RecoveryRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                    Action = "unfundedRelease",
                });
                recovery.Success.Should().BeTrue(recovery.Error);
            }

            Log(null, $"========== {nameof(BigInvest)} PASSED ==========");
        }
        finally
        {
            foreach (var kvp in hosts.Where(h => !string.Equals(h.Key, FounderProfile, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                await kvp.Value.DisposeAsync();
            }
        }
    }

    private static async Task<ITestHost> GetOrCreateHostAsync(
        IDictionary<string, ITestHost> hosts,
        string profileName)
    {
        if (hosts.TryGetValue(profileName, out var existing))
        {
            return existing;
        }

        var host = await TestHostFactory.LaunchAsync(profileName);
        await host.Client.WipeDataAsync();
        await host.Client.EnableDebugModeAsync();
        hosts[profileName] = host;
        return host;
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

public class BigFundTest
{
    private const string TestName = "BigFund";
    private const string FounderProfile = TestName + "-Founder";
    private const int TotalInvestors = 15;
    private const int BelowThresholdCount = 7;
    private const int AboveThresholdCount = 8;

    private sealed record ProjectHandle(string RunId, string ProjectName, string ProjectIdentifier, string FounderWalletId);
    private sealed record InvestorConfig(string ProfileName, string AmountBtc, bool ExpectFounderApproval, int TargetPatternStageCount);

    private static string InvestorProfile(int index) => $"{TestName}-Investor{index}";

    private static List<InvestorConfig> BuildInvestorConfigs()
    {
        var configs = new List<InvestorConfig>();
        for (int i = 1; i <= BelowThresholdCount; i++)
        {
            configs.Add(new InvestorConfig(InvestorProfile(i), "0.001", false, i <= 5 ? 6 : 3));
        }

        for (int i = BelowThresholdCount + 1; i <= TotalInvestors; i++)
        {
            configs.Add(new InvestorConfig(InvestorProfile(i), "0.02", true, i <= 12 ? 6 : 3));
        }

        return configs;
    }

    [Fact]
    public async Task BigFund()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Big Fund {runId}";
        var projectAbout = $"{TestName} run {runId}. 15-investor fund project with mixed thresholds and installment patterns.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";
        var payoutDay = DateTime.UtcNow.DayOfWeek.ToString();
        var investors = BuildInvestorConfigs();

        Log(null, $"========== STARTING {nameof(BigFund)} ==========");
        Log(null, $"Run ID: {runId}");

        await using var founderHost = await TestHostFactory.LaunchAsync(FounderProfile);
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
            InstallmentCount = 6,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);

        var project = new ProjectHandle(runId, projectName, createdProject.ProjectIdentifier!, createdProject.OwnerWalletId!);

        var hosts = new Dictionary<string, ITestHost>(StringComparer.OrdinalIgnoreCase)
        {
            [FounderProfile] = founderHost,
        };

        try
        {
            var approvedSoFar = 0;
            foreach (var inv in investors)
            {
                Log(null, $"Processing {inv.ProfileName}: amount={inv.AmountBtc}, approval={inv.ExpectFounderApproval}, pattern={inv.TargetPatternStageCount}");
                var host = await GetOrCreateHostAsync(hosts, inv.ProfileName, enableDebugMode: false);

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
                    ExpectFounderApproval = inv.ExpectFounderApproval,
                    TargetPatternStageCount = inv.TargetPatternStageCount,
                });
                invest.Success.Should().BeTrue(invest.Error);

                if (inv.ExpectFounderApproval)
                {
                    approvedSoFar++;
                    var approve = await founderHost.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
                    {
                        ProjectIdentifier = project.ProjectIdentifier,
                        ExpectedCount = approvedSoFar,
                        Batch = false,
                    });
                    approve.Success.Should().BeTrue(approve.Error);

                    var confirm = await host.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
                    {
                        ProjectIdentifier = project.ProjectIdentifier,
                    });
                    confirm.Success.Should().BeTrue(confirm.Error);
                    confirm.Step.Should().Be(3);
                }
            }

            var claim = await founderHost.Client.ClaimStageAsync(new ClaimStageRequest
            {
                ProjectIdentifier = project.ProjectIdentifier,
                StageNumber = 1,
                ExpectedUtxoCount = TotalInvestors,
            });
            claim.Success.Should().BeTrue(claim.Error);

            foreach (var inv in investors.Where(i => i.ExpectFounderApproval))
            {
                var host = hosts[inv.ProfileName];
                var recover = await host.Client.ExecuteRecoveryAsync(new RecoveryRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                    Action = "recovery",
                });
                recover.Success.Should().BeTrue(recover.Error);

                var penaltyRelease = await host.Client.ExecuteRecoveryAsync(new RecoveryRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                    Action = "penaltyRelease",
                });
                penaltyRelease.Success.Should().BeTrue(penaltyRelease.Error);
            }

            foreach (var inv in investors.Where(i => !i.ExpectFounderApproval))
            {
                var host = hosts[inv.ProfileName];
                var claimEnd = await host.Client.ExecuteRecoveryAsync(new RecoveryRequest
                {
                    ProjectIdentifier = project.ProjectIdentifier,
                    Action = "belowThreshold",
                });
                claimEnd.Success.Should().BeTrue(claimEnd.Error);
            }

            Log(null, $"========== {nameof(BigFund)} PASSED ==========");
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
        string profileName,
        bool enableDebugMode)
    {
        if (hosts.TryGetValue(profileName, out var existing))
        {
            return existing;
        }

        var host = await TestHostFactory.LaunchAsync(profileName);
        await host.Client.WipeDataAsync();
        if (enableDebugMode)
        {
            await host.Client.EnableDebugModeAsync();
        }

        hosts[profileName] = host;
        return host;
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}

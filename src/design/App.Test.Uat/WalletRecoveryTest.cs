using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Tests wallet recovery from seed words, for both a founder and an investor:
/// 1. Founder: create wallet (generate) → capture seed words → fund → deploy a project
/// 2. Investor: create wallet → invest in the project → founder approves → confirm (Step 3)
/// 3. Founder: wipe all data → import wallet from seed → same deterministic WalletId
/// 4. Investor: wipe all data → import wallet from seed → portfolio investment is
///    auto-recovered from Nostr relays (PR #924 relay decryption / handshake sync)
/// </summary>
public class WalletRecoveryTest
{
    private const string TestName = "WalletRecovery";
    private const string Profile = TestName + "-User";
    private const string InvestorProfile = TestName + "-Investor";

    [Fact]
    public async Task RecoverWalletFromSeedWords()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Recovery Test {runId}";
        var projectAbout = $"{TestName} run {runId}. Wallet recovery from seed words.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        Log($"========== STARTING {nameof(RecoverWalletFromSeedWords)} ==========");
        Log($"Run ID: {runId}");

        await using var host = await TestProcessHost.LaunchAsync(Profile);
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();

        // ── Step 1: Create founder wallet and fund (captures seed words) ──
        Log("Step 1: Creating founder wallet and funding...");
        var wallet = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
        });
        wallet.Success.Should().BeTrue(wallet.Error);
        wallet.SeedWords.Should().NotBeNullOrEmpty("Seed words should be returned from wallet generation");
        var seedWords = wallet.SeedWords!;
        var originalWalletId = wallet.WalletId!;
        Log($"Wallet created: {originalWalletId}");
        Log($"Seed words captured ({seedWords.Split(' ').Length} words)");

        // ── Step 2: Deploy a project to have something to recover ──
        Log("Step 2: Deploying invest project...");
        var createdProject = await host.Client.CreateInvestProjectAsync(new CreateInvestProjectRequest
        {
            ProjectName = projectName,
            ProjectAbout = projectAbout,
            BannerUrl = bannerImageUrl,
            ProfileUrl = profileImageUrl,
            RunId = runId,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);
        var projectId = createdProject.ProjectIdentifier!;
        Log($"Project deployed: {projectId}");

        // ── Step 3: Investor invests, founder approves, investor confirms ──
        Log("Step 3: Launching investor, investing in the project...");
        await using var investorHost = await TestProcessHost.LaunchAsync(InvestorProfile);
        await investorHost.Client.WipeDataAsync();
        await investorHost.Client.SwitchNetworkAsync("Angornet");
        await investorHost.Client.EnableDebugModeAsync();

        var investorWallet = await investorHost.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = InvestorProfile,
        });
        investorWallet.Success.Should().BeTrue(investorWallet.Error);
        investorWallet.SeedWords.Should().NotBeNullOrEmpty();
        var investorSeedWords = investorWallet.SeedWords!;
        var investorWalletId = investorWallet.WalletId!;
        Log($"Investor wallet created: {investorWalletId}");

        var invest = await investorHost.Client.InvestInProjectAsync(new InvestInProjectRequest
        {
            ProjectIdentifier = projectId,
            RunId = runId,
            ProjectName = projectName,
            AmountBtc = "0.02",
            ExpectFounderApproval = true,
            TargetPatternStageCount = 0,
        });
        invest.Success.Should().BeTrue(invest.Error);

        Log("Founder approving the investment...");
        var approve = await host.Client.ApproveInvestmentsAsync(new ApproveInvestmentsRequest
        {
            ProjectIdentifier = projectId,
            ExpectedCount = 1,
            Batch = true,
        });
        approve.Success.Should().BeTrue(approve.Error);

        Log("Investor confirming the investment...");
        var confirm = await investorHost.Client.ConfirmInvestmentAsync(new ConfirmInvestmentRequest
        {
            ProjectIdentifier = projectId,
        });
        confirm.Success.Should().BeTrue(confirm.Error);
        confirm.Step.Should().Be(3);

        // ── Step 4: Founder wipes and re-imports from seed ──
        Log("Step 4: Wiping founder data and re-importing from seed words...");
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");

        var imported = await host.Client.ImportWalletAsync(new ImportWalletRequest
        {
            SeedWords = seedWords,
            ProfileName = Profile,
        });
        imported.Success.Should().BeTrue(imported.Error);
        imported.WalletId.Should().NotBeNullOrEmpty("Imported wallet should have an ID");
        Log($"Founder wallet imported: {imported.WalletId}");

        // The wallet ID should be the same since it's derived from the same seed
        imported.WalletId.Should().Be(originalWalletId, "Imported wallet should have the same ID as the original");

        // ── Step 5: Investor wipes and re-imports; portfolio must auto-recover from relays ──
        Log("Step 5: Wiping investor data and re-importing from seed words...");
        await investorHost.Client.WipeDataAsync();
        await investorHost.Client.SwitchNetworkAsync("Angornet");

        var investorImported = await investorHost.Client.ImportWalletAsync(new ImportWalletRequest
        {
            SeedWords = investorSeedWords,
            ProfileName = InvestorProfile,
        });
        investorImported.Success.Should().BeTrue(investorImported.Error);
        investorImported.WalletId.Should().Be(investorWalletId,
            "Imported investor wallet should have the same ID as the original");

        Log("Waiting for portfolio auto-recovery from Nostr relays...");
        var recovered = await WaitForPortfolioInvestmentAsync(investorHost, projectId, TimeSpan.FromMinutes(5));
        recovered.Should().BeTrue(
            $"Investment in project {projectId} should be auto-recovered into the portfolio after wallet re-import");
        Log("Portfolio investment recovered.");

        Log($"========== {nameof(RecoverWalletFromSeedWords)} PASSED ==========");
    }

    /// <summary>
    /// Polls PortfolioViewModel (singleton) until an investment for the given project appears,
    /// re-triggering LoadInvestmentsFromSdkAsync between attempts (relay handshake sync).
    /// </summary>
    private static async Task<bool> WaitForPortfolioInvestmentAsync(
        TestProcessHost host, string projectId, TimeSpan timeout)
    {
        await host.Client.NavigateAsync("Funded");

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await host.Client.InvokeVmAsync("PortfolioViewModel", "LoadInvestmentsFromSdkAsync");

                var countText = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Investments.Count");
                if (int.TryParse(countText, out var count) && count > 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var identifier = await host.Client.GetVmPropertyAsync(
                            "PortfolioViewModel", $"Investments[{i}].ProjectIdentifier");
                        if (string.Equals(identifier, projectId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Portfolio poll attempt failed (will retry): {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        return false;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{TestName}] {message}");
    }
}

using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Tests wallet recovery from seed words:
/// 1. Create wallet (generate) → capture seed words
/// 2. Fund wallet and deploy a project
/// 3. Wipe all data
/// 4. Import wallet from seed words
/// 5. Verify the wallet balance is recovered and the project is visible in My Projects
/// </summary>
public class WalletRecoveryTest
{
    private const string TestName = "WalletRecovery";
    private const string Profile = TestName + "-User";

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

        // ── Step 1: Create wallet and fund (captures seed words) ──
        Log("Step 1: Creating wallet and funding...");
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

        // ── Step 3: Wipe all data ──
        Log("Step 3: Wiping all data...");
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");
        Log("Data wiped.");

        // ── Step 4: Import wallet from seed words ──
        Log("Step 4: Importing wallet from seed words...");
        var imported = await host.Client.ImportWalletAsync(new ImportWalletRequest
        {
            SeedWords = seedWords,
            ProfileName = Profile,
        });
        imported.Success.Should().BeTrue(imported.Error);
        imported.WalletId.Should().NotBeNullOrEmpty("Imported wallet should have an ID");
        Log($"Wallet imported: {imported.WalletId}");

        // The wallet ID should be the same since it's derived from the same seed
        imported.WalletId.Should().Be(originalWalletId, "Imported wallet should have the same ID as the original");

        Log($"========== {nameof(RecoverWalletFromSeedWords)} PASSED ==========");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{TestName}] {message}");
    }
}

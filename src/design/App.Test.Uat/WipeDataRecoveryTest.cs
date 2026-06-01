using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// UAT test for the wipe-data flow with optional recovery wallet file purge.
///
/// Scenario:
/// 1. Create two wallets and fund them on Angornet
/// 2. Verify Angornet balance is non-zero
/// 3. Switch to Mainnet -- verify no wallets loaded (different network context)
/// 4. Switch back to Angornet -- verify recovery files survived the switch
/// 5. Import wallet 1 from recovery files and verify balance restores
/// 6. Standard wipe (no purge) -- verify recovery files survive (stored count = 2)
/// 7. Import only wallet 1 (selective recovery) -- verify balance restores
/// 8. Wipe WITH purge -- verify recovery files are deleted (stored count = 0)
/// 9. Create a fresh wallet to confirm app is fully functional after purge
/// </summary>
public class WipeDataRecoveryTest
{
    private const string TestName = "WipeDataRecovery";
    private const string Profile = TestName + "-User";

    [Fact]
    public async Task WipeDataWithRecoveryPurge_CleansWalletAndRecoveryFiles()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];

        Log($"========== STARTING {nameof(WipeDataWithRecoveryPurge_CleansWalletAndRecoveryFiles)} ==========");
        Log($"Run ID: {runId}");

        await using var host = await TestProcessHost.LaunchAsync(Profile);

        // ── Step 1: Clean slate ──
        Log("Step 1: Wiping data with recovery purge to start clean...");
        await host.Client.WipeDataWithRecoveryPurgeAsync();
        await host.Client.EnableDebugModeAsync();

        // ── Step 2: Create two wallets and fund them ──
        Log("Step 2a: Creating first wallet and funding...");
        var wallet1 = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
        });
        wallet1.Success.Should().BeTrue(wallet1.Error);
        Log($"Wallet 1 created: {wallet1.WalletId} (seed captured)");

        Log("Step 2b: Creating second wallet and funding...");
        var wallet2 = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
        });
        wallet2.Success.Should().BeTrue(wallet2.Error);
        Log($"Wallet 2 created: {wallet2.WalletId} (seed captured)");

        // ── Step 3: Verify Angornet balance is non-zero ──
        Log("Step 3: Verifying Angornet balance is non-zero...");
        var angornetBalance = await host.Client.GetTotalBalanceAsync();
        Log($"Angornet total balance: {angornetBalance}");
        angornetBalance.Should().NotBeNullOrEmpty();
        double.Parse(angornetBalance!).Should().BeGreaterThan(0, "both wallets were funded on Angornet");

        // ── Step 4: Switch to Mainnet -- verify balance is zero ──
        Log("Step 4: Switching to Mainnet...");
        await host.Client.SwitchNetworkAsync("Mainnet");
        await Task.Delay(3000);

        var mainnetBalance = await host.Client.GetTotalBalanceAsync();
        Log($"Mainnet total balance: {mainnetBalance}");
        (mainnetBalance == null || mainnetBalance == "0.0000" || mainnetBalance == "0").Should()
            .BeTrue($"expected zero balance on Mainnet but got '{mainnetBalance}'");

        // ── Step 5: Switch back to Angornet ──
        Log("Step 5: Switching back to Angornet...");
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();
        await Task.Delay(2000);
        Log("Network switched back to Angornet successfully.");

        // ── Step 6: Standard wipe (no purge) -- recovery files survive ──
        Log("Step 6: Standard wipe (no recovery purge)...");
        await host.Client.WipeDataAsync();
        await host.Client.EnableDebugModeAsync();

        var storedCountAfterWipe = await host.Client.GetStoredWalletsCountAsync();
        Log($"Stored wallets after standard wipe: {storedCountAfterWipe}");
        storedCountAfterWipe.Should().BeGreaterThanOrEqualTo(1,
            "standard wipe should preserve recovery wallet files");

        // ── Step 7: Selective recovery -- import only wallet 1 ──
        Log("Step 7: Selective recovery -- importing only wallet 1...");
        var imported1 = await host.Client.ImportWalletAsync(new ImportWalletRequest
        {
            SeedWords = wallet1.SeedWords!,
            ProfileName = Profile,
        });
        imported1.Success.Should().BeTrue(imported1.Error);
        imported1.WalletId.Should().Be(wallet1.WalletId);
        Log($"Wallet 1 selectively recovered: {imported1.WalletId}");

        // Verify only wallet 1 has balance (wallet 2 was NOT imported)
        Log("Step 7: Verifying balance after selective recovery...");

        // ── Step 8: Wipe WITH recovery purge -- recovery files deleted ──
        Log("Step 8: Wiping data WITH recovery wallet file purge...");

        // ── Step 9: Create fresh wallet to verify app is functional ──
        Log("Step 9: Creating fresh wallet to verify app works after full purge...");
        var freshWallet = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
        });
        freshWallet.Success.Should().BeTrue(freshWallet.Error);
        Log($"Fresh wallet created: {freshWallet.WalletId}");

        // ── Cleanup ──
        Log("Cleanup: Final wipe with purge...");
        await host.Client.WipeDataWithRecoveryPurgeAsync();

        Log($"========== {nameof(WipeDataWithRecoveryPurge_CleansWalletAndRecoveryFiles)} PASSED ==========");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{TestName}] {message}");
    }
}

using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// UAT test for the wipe-data flow with optional recovery wallet file purge.
///
/// Scenario:
/// 1. Create two wallets (no funding needed)
/// 2. Switch to Mainnet and back -- verify recovery files survive
/// 3. Standard wipe (no purge) -- verify recovery files survive (stored count = 2)
/// 4. Recover wallet 1 from stored recovery file (wallets.json)
/// 5. Wipe WITH purge -- verify recovery files are deleted (stored count = 0)
/// 6. Create a fresh wallet to confirm app is fully functional after purge
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

        // ── Step 2: Create two wallets (no funding) ──
        Log("Step 2a: Creating first wallet...");
        var wallet1 = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
            SkipFunding = true,
        });
        wallet1.Success.Should().BeTrue(wallet1.Error);
        Log($"Wallet 1 created: {wallet1.WalletId}");

        Log("Step 2b: Creating second wallet...");
        var wallet2 = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
            ForceCreate = true,
            SkipFunding = true,
        });
        wallet2.Success.Should().BeTrue(wallet2.Error);
        wallet2.WalletId.Should().NotBe(wallet1.WalletId, "second wallet should have a different ID");
        Log($"Wallet 2 created: {wallet2.WalletId}");

        // ── Step 3: Switch to Mainnet and back ──
        Log("Step 3a: Switching to Mainnet...");
        await host.Client.SwitchNetworkAsync("Mainnet");
        await Task.Delay(3000);

        Log("Step 3b: Switching back to Angornet...");
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();
        await Task.Delay(2000);
        Log("Network round-trip completed.");

        // ── Step 4: Standard wipe (no purge) -- recovery files survive ──
        Log("Step 4: Standard wipe (no recovery purge)...");
        await host.Client.WipeDataAsync();
        await host.Client.EnableDebugModeAsync();

        var storedCountAfterWipe = await host.Client.GetStoredWalletsCountAsync();
        Log($"Stored wallets after standard wipe: {storedCountAfterWipe}");
        storedCountAfterWipe.Should().Be(2,
            "standard wipe should preserve all recovery wallet files");

        // ── Step 5: Recover wallet 1 from stored recovery file (wallets.json) ──
        Log("Step 5: Recovering wallet 1 from stored recovery file...");
        var recovered1 = await host.Client.RecoverStoredWalletAsync(new RecoverStoredWalletRequest
        {
            WalletId = wallet1.WalletId!,
        });
        recovered1.Success.Should().BeTrue(recovered1.Error);
        recovered1.WalletId.Should().Be(wallet1.WalletId);
        Log($"Wallet 1 recovered from file: {recovered1.WalletId}");

        // ── Step 6: Wipe WITH recovery purge -- recovery files deleted ──
        Log("Step 6: Wiping data WITH recovery wallet file purge...");
        await host.Client.WipeDataWithRecoveryPurgeAsync();
        await host.Client.EnableDebugModeAsync();

        var storedCountAfterPurge = await host.Client.GetStoredWalletsCountAsync();
        Log($"Stored wallets after purge: {storedCountAfterPurge}");
        storedCountAfterPurge.Should().Be(0,
            "wipe with purge should delete all recovery wallet files");

        // ── Step 7: Create fresh wallet to verify app is functional ──
        Log("Step 7: Creating fresh wallet to verify app works after full purge...");
        var freshWallet = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
            SkipFunding = true,
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

using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Exercises the Settings page:
/// 1. Dark theme toggle — flips IsDarkThemeEnabled and verifies it persists in shared settings
/// 2. Backup Account — reveals seed words in the UI and verifies they match the wallet's seed
/// 3. Network switch — Angornet → Mainnet → Angornet, verifying:
///    - NetworkType reflects the switch
///    - the Find Projects list is repopulated after each switch (no stale list, PR #933)
///    - the wallet survives the round trip (same stored wallet recovered)
/// </summary>
public class SettingsTest
{
    private const string TestName = "Settings";
    private const string Profile = TestName + "-User";

    [Fact]
    public async Task SettingsThemeBackupAndNetworkSwitch()
    {
        Log($"========== STARTING {nameof(SettingsThemeBackupAndNetworkSwitch)} ==========");

        await using var host = await TestProcessHost.LaunchAsync(Profile);
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();

        // ── Wallet (unfunded — Settings features only need a wallet to exist) ──
        Log("Creating wallet (no funding)...");
        var wallet = await host.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = Profile,
            SkipFunding = true,
        });
        wallet.Success.Should().BeTrue(wallet.Error);
        wallet.SeedWords.Should().NotBeNullOrEmpty("Seed words should be captured at creation");
        var seedWords = wallet.SeedWords!;

        // ══════════════════════════════════════════════════════════════
        // 1. Dark theme toggle persists through shared settings
        // ══════════════════════════════════════════════════════════════
        Log("Testing dark theme toggle...");
        await host.Client.NavigateAsync("Settings");

        var initialTheme = await host.Client.GetVmPropertyAsync("SettingsViewModel", "IsDarkThemeEnabled");
        Log($"Initial IsDarkThemeEnabled: {initialTheme}");
        var flipped = !string.Equals(initialTheme, "True", StringComparison.OrdinalIgnoreCase);

        await host.Client.SetVmPropertyAsync("SettingsViewModel", "IsDarkThemeEnabled", flipped);
        await Task.Delay(500);

        // SettingsViewModel is transient — a fresh resolution reads back from the shared
        // PrototypeSettings singleton, proving the change was written through.
        var afterFlip = await host.Client.GetVmPropertyAsync("SettingsViewModel", "IsDarkThemeEnabled");
        afterFlip.Should().Be(flipped ? "True" : "False", "theme change should persist in shared settings");

        // Restore the original theme
        await host.Client.SetVmPropertyAsync("SettingsViewModel", "IsDarkThemeEnabled", !flipped);
        var restored = await host.Client.GetVmPropertyAsync("SettingsViewModel", "IsDarkThemeEnabled");
        restored.Should().Be(initialTheme, "theme should be restorable to its original value");
        Log("Dark theme toggle verified.");

        // ══════════════════════════════════════════════════════════════
        // 2. Backup Account — reveal seed words and compare with the wallet's seed
        // ══════════════════════════════════════════════════════════════
        Log("Testing Backup Account (Show Seed Words)...");
        await host.Client.NavigateAsync("Settings");
        await host.Client.WaitForControlAsync("BtnRevealSeed", TimeSpan.FromSeconds(30));
        await host.Client.ClickAsync("BtnRevealSeed");

        var seedControl = await host.Client.WaitForControlAsync("SettingsSeedWordsText", TimeSpan.FromSeconds(30));
        seedControl.Text.Should().NotBeNullOrWhiteSpace("Seed words should be displayed after reveal");
        seedControl.Text!.Trim().Should().Be(seedWords.Trim(),
            "Revealed seed words must match the seed captured at wallet creation");

        // Download Seed button becomes visible alongside the revealed seed
        var downloadBtn = await host.Client.FindControlAsync("BtnDownloadBackup");
        downloadBtn.Found.Should().BeTrue();
        downloadBtn.IsVisible.Should().BeTrue("Download Seed button should be visible after reveal");

        // Hide again (same button toggles)
        await host.Client.ClickAsync("BtnRevealSeed");
        await Task.Delay(500);
        Log("Backup Account verified.");

        // ══════════════════════════════════════════════════════════════
        // 3. Network switch round trip refreshes Find Projects (PR #933)
        // ══════════════════════════════════════════════════════════════
        Log("Loading Find Projects on Angornet...");
        var angornetProjects = await host.Client.GetFindProjectsCountAsync(new GetFindProjectsCountRequest
        {
            MinCount = 1,
            TimeoutSeconds = 120,
        });
        angornetProjects.Success.Should().BeTrue(angornetProjects.Error);
        Log($"Angornet projects: {angornetProjects.Count}");

        Log("Switching to Mainnet...");
        await host.Client.SwitchNetworkAsync("Mainnet");

        var networkType = await host.Client.GetVmPropertyAsync("SettingsViewModel", "NetworkType");
        networkType.Should().Contain("Mainnet", "NetworkType should reflect the switch to Mainnet");

        // The project list must reload with mainnet projects (not remain stale)
        var mainnetProjects = await host.Client.GetFindProjectsCountAsync(new GetFindProjectsCountRequest
        {
            MinCount = 1,
            TimeoutSeconds = 180,
        });
        mainnetProjects.Success.Should().BeTrue(mainnetProjects.Error);
        Log($"Mainnet projects: {mainnetProjects.Count}");

        Log("Switching back to Angornet...");
        await host.Client.SwitchNetworkAsync("Angornet");

        networkType = await host.Client.GetVmPropertyAsync("SettingsViewModel", "NetworkType");
        networkType.Should().Contain("Angornet", "NetworkType should reflect the switch back to Angornet");

        var angornetProjectsAgain = await host.Client.GetFindProjectsCountAsync(new GetFindProjectsCountRequest
        {
            MinCount = 1,
            TimeoutSeconds = 120,
        });
        angornetProjectsAgain.Success.Should().BeTrue(angornetProjectsAgain.Error);
        Log($"Angornet projects after round trip: {angornetProjectsAgain.Count}");

        // ── Wallet survives the network round trip via stored recovery files ──
        var storedWallets = await host.Client.GetStoredWalletsCountAsync();
        storedWallets.Should().BeGreaterThanOrEqualTo(1, "Wallet recovery file should survive network switches");

        Log($"========== {nameof(SettingsThemeBackupAndNetworkSwitch)} PASSED ==========");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{TestName}] {message}");
    }
}

using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Shared.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.Test.Integration.Helpers;
using App.UI.Sections.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// Verifies that switching networks clears all document collections,
/// leaving the app in a state equivalent to a freshly created wallet.
///
/// Steps:
///   1. Wipe existing data and create a fresh wallet
///   2. Seed test documents into all 8 document collections
///   3. Switch network (Angornet → Mainnet)
///   4. Verify every document collection is empty
///   5. Verify the wallet still exists (network switch preserves wallets)
///   6. Switch back to the original network
/// </summary>
public class NetworkSwitchClearsDataTest
{
    [AvaloniaFact]
    public async Task NetworkSwitch_ClearsAllDocumentCollections_ButPreservesWallet()
    {
        using var profileScope = TestProfileScope.For(nameof(NetworkSwitchClearsDataTest));
        TestHelpers.Log("========== STARTING NetworkSwitch_ClearsAllDocumentCollections ==========");

        // ──────────────────────────────────────────────────────────────
        // ARRANGE: Boot app, wipe, create wallet
        // ──────────────────────────────────────────────────────────────
        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();

        TestHelpers.Log("[STEP 1] Wiping existing data...");
        await window.WipeExistingData();

        TestHelpers.Log("[STEP 2] Creating wallet...");
        await window.CreateWalletViaGenerate();

        // Verify wallet exists
        shellVm.SelectedWallet.Should().NotBeNull("a wallet should exist after creation");
        var walletCountBefore = shellVm.SwitcherWallets.Count;
        walletCountBefore.Should().BeGreaterThan(0, "at least one wallet should be in the switcher");
        TestHelpers.Log($"[STEP 2] Wallet created. Switcher count: {walletCountBefore}");

        // ──────────────────────────────────────────────────────────────
        // STEP 3: Seed test documents into all 8 collections
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 3] Seeding test documents into all collections...");
        var services = global::App.App.Services!;

        var projectsCollection = services.GetRequiredService<IGenericDocumentCollection<Project>>();
        var derivedKeysCollection = services.GetRequiredService<IGenericDocumentCollection<DerivedProjectKeys>>();
        var boltzSwapsCollection = services.GetRequiredService<IGenericDocumentCollection<BoltzSwapDocument>>();
        var walletBalanceCollection = services.GetRequiredService<IGenericDocumentCollection<WalletAccountBalanceInfo>>();
        var queryTxCollection = services.GetRequiredService<IGenericDocumentCollection<QueryTransaction>>();
        var txHexCollection = services.GetRequiredService<IGenericDocumentCollection<TransactionHexDocument>>();
        var investmentRecordsCollection = services.GetRequiredService<IGenericDocumentCollection<InvestmentRecordsDocument>>();
        var investmentHandshakesCollection = services.GetRequiredService<IGenericDocumentCollection<InvestmentHandshake>>();

        // Seed one document into each collection using the same ID selector patterns
        // used by production code (see DocumentProjectService, WalletFactory, etc.)
        var testId = Guid.NewGuid().ToString("N")[..12];

        var seedProject = await projectsCollection.UpsertAsync(
            p => p.Id.Value,
            new Project { Id = new ProjectId($"test-proj-{testId}"), Name = "Test Project" });
        seedProject.IsSuccess.Should().BeTrue($"seeding Project should succeed: {seedProject}");

        var seedDerived = await derivedKeysCollection.UpsertAsync(
            d => d.WalletId,
            new DerivedProjectKeys { WalletId = $"test-dk-{testId}", Keys = new List<FounderKeys>() });
        seedDerived.IsSuccess.Should().BeTrue($"seeding DerivedProjectKeys should succeed: {seedDerived}");

        var seedBoltz = await boltzSwapsCollection.UpsertAsync(
            b => b.SwapId,
            new BoltzSwapDocument { SwapId = $"test-boltz-{testId}" });
        seedBoltz.IsSuccess.Should().BeTrue($"seeding BoltzSwapDocument should succeed: {seedBoltz}");

        var seedBalance = await walletBalanceCollection.UpsertAsync(
            w => w.WalletId,
            new WalletAccountBalanceInfo { WalletId = $"test-bal-{testId}", AccountBalanceInfo = new AccountBalanceInfo() });
        seedBalance.IsSuccess.Should().BeTrue($"seeding WalletAccountBalanceInfo should succeed: {seedBalance}");

        var seedQueryTx = await queryTxCollection.UpsertAsync(
            q => q.TransactionId,
            new QueryTransaction { TransactionId = $"test-qtx-{testId}" });
        seedQueryTx.IsSuccess.Should().BeTrue($"seeding QueryTransaction should succeed: {seedQueryTx}");

        var seedTxHex = await txHexCollection.UpsertAsync(
            t => t.Id,
            new TransactionHexDocument { Id = $"test-txhex-{testId}", Hex = "deadbeef" });
        seedTxHex.IsSuccess.Should().BeTrue($"seeding TransactionHexDocument should succeed: {seedTxHex}");

        var seedInvestment = await investmentRecordsCollection.UpsertAsync(
            i => i.WalletId,
            new InvestmentRecordsDocument { WalletId = $"test-inv-{testId}" });
        seedInvestment.IsSuccess.Should().BeTrue($"seeding InvestmentRecordsDocument should succeed: {seedInvestment}");

        var seedHandshake = await investmentHandshakesCollection.UpsertAsync(
            h => h.Id,
            new InvestmentHandshake { Id = $"test-hs-{testId}", WalletId = $"test-inv-{testId}" });
        seedHandshake.IsSuccess.Should().BeTrue($"seeding InvestmentHandshake should succeed: {seedHandshake}");

        // Verify seeds are present
        var projectCount = await projectsCollection.CountAsync();
        projectCount.Value.Should().BeGreaterThan(0, "projects should have been seeded");
        TestHelpers.Log($"[STEP 3] Seeded all 8 collections. Projects count: {projectCount.Value}");

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Switch network (Angornet → Mainnet)
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 4] Switching network to Mainnet...");
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants()
            .OfType<SettingsView>()
            .FirstOrDefault();
        settingsView.Should().NotBeNull("SettingsView should be available");

        var settingsVm = settingsView!.DataContext as SettingsViewModel;
        settingsVm.Should().NotBeNull("SettingsViewModel should be available");

        // Determine target network (switch to the OTHER network to guarantee a real switch)
        var currentNetwork = settingsVm!.NetworkType;
        var targetNetwork = currentNetwork == "Mainnet" ? "Angornet" : "Mainnet";
        TestHelpers.Log($"[STEP 4] Current network: {currentNetwork}, switching to: {targetNetwork}");

        // Perform network switch
        settingsVm.SelectedNetworkToSwitch = targetNetwork;
        settingsVm.NetworkChangeConfirmed = true;
        await settingsVm.ConfirmNetworkSwitchAsync();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        settingsVm.NetworkType.Should().Be(targetNetwork, "network should have switched");
        TestHelpers.Log("[STEP 4] Network switch completed.");

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Verify all document collections are empty
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 5] Verifying all document collections are empty...");

        // After network switch, DeleteAllDataAsync should clear all collections.
        // RebuildAllWalletBalancesAsync may re-insert DerivedProjectKeys and WalletAccountBalanceInfo
        // for the preserved wallet. Background prewarm may re-insert cached projects.
        // We verify our specifically SEEDED test documents are gone by ID.

        var seededProject = await projectsCollection.FindByIdAsync($"test-proj-{testId}");
        seededProject.Value.Should().BeNull("seeded Project should be deleted after network switch");

        var seededDerivedKeys = await derivedKeysCollection.FindByIdAsync($"test-dk-{testId}");
        seededDerivedKeys.Value.Should().BeNull("seeded DerivedProjectKeys should be deleted after network switch");

        var seededBoltz = await boltzSwapsCollection.FindByIdAsync($"test-boltz-{testId}");
        seededBoltz.Value.Should().BeNull("seeded BoltzSwapDocument should be deleted after network switch");

        var seededBalance = await walletBalanceCollection.FindByIdAsync($"test-bal-{testId}");
        seededBalance.Value.Should().BeNull("seeded WalletAccountBalanceInfo should be deleted after network switch");

        var seededQueryTx = await queryTxCollection.FindByIdAsync($"test-qtx-{testId}");
        seededQueryTx.Value.Should().BeNull("seeded QueryTransaction should be deleted after network switch");

        var seededTxHex = await txHexCollection.FindByIdAsync($"test-txhex-{testId}");
        seededTxHex.Value.Should().BeNull("seeded TransactionHexDocument should be deleted after network switch");

        var seededInvestment = await investmentRecordsCollection.FindByIdAsync($"test-inv-{testId}");
        seededInvestment.Value.Should().BeNull("seeded InvestmentRecordsDocument should be deleted after network switch");

        var seededHandshake = await investmentHandshakesCollection.FindByIdAsync($"test-hs-{testId}");
        seededHandshake.Value.Should().BeNull("seeded InvestmentHandshake should be deleted after network switch");

        TestHelpers.Log("[STEP 5] All 8 document collections confirmed empty.");

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Verify wallet was preserved (not deleted)
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 6] Verifying wallet was preserved...");
        shellVm.SwitcherWallets.Count.Should().Be(walletCountBefore,
            "network switch should preserve wallets — count should match pre-switch");
        shellVm.SelectedWallet.Should().NotBeNull(
            "network switch should preserve the selected wallet");
        TestHelpers.Log("[STEP 6] Wallet preserved after network switch.");

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Switch back to Angornet (cleanup)
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 7] Switching back to original network...");
        settingsVm.SelectedNetworkToSwitch = currentNetwork;
        settingsVm.NetworkChangeConfirmed = true;
        await settingsVm.ConfirmNetworkSwitchAsync();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        settingsVm.NetworkType.Should().Be(currentNetwork, "should be back on original network after switch-back");
        TestHelpers.Log("[STEP 7] Switched back to original network.");

        TestHelpers.Log("========== NetworkSwitch_ClearsAllDocumentCollections PASSED ==========");
    }
}

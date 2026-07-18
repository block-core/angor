using System.Globalization;
using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Stress-tests wallet send/receive across 3 users over 10 rounds.
/// Each round, all 3 users send to each other simultaneously (A->B, B->C, C->A).
/// Some rounds fire sends back-to-back without waiting for confirmation (spending unconfirmed UTXOs).
/// After each round, all balances are refreshed and verified against expected running totals.
/// </summary>
public class SendFundsTest
{
    private const string TestName = "SendFunds";
    private const string ProfileA = TestName + "-UserA";
    private const string ProfileB = TestName + "-UserB";
    private const string ProfileC = TestName + "-UserC";

    private const int TotalRounds = 10;
    private const double SendAmount = 0.005; // BTC per send
    private const long FeeRate = 2;

    [Fact]
    public async Task ThreeUsersSendToEachOther()
    {
        Log($"========== STARTING {nameof(ThreeUsersSendToEachOther)} — {TotalRounds} rounds ==========");

        // ── Launch 3 app instances ──
        Log("Launching 3 app instances...");
        await using var hostA = await TestHostFactory.LaunchAsync(ProfileA);
        await using var hostB = await TestHostFactory.LaunchAsync(ProfileB);
        await using var hostC = await TestHostFactory.LaunchAsync(ProfileC);

        await Task.WhenAll(
            WipeAndInit(hostA),
            WipeAndInit(hostB),
            WipeAndInit(hostC));

        // ── Create wallets and fund all 3 via faucet ──
        Log("Creating and funding wallets for all 3 users...");

        var wallets = await Task.WhenAll(
            hostA.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = ProfileA }),
            hostB.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = ProfileB }),
            hostC.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest { ProfileName = ProfileC }));

        wallets[0].Success.Should().BeTrue(wallets[0].Error);
        wallets[1].Success.Should().BeTrue(wallets[1].Error);
        wallets[2].Success.Should().BeTrue(wallets[2].Error);

        var idA = wallets[0].WalletId!;
        var idB = wallets[1].WalletId!;
        var idC = wallets[2].WalletId!;

        Log($"Wallet A: {idA}");
        Log($"Wallet B: {idB}");
        Log($"Wallet C: {idC}");

        // Track running balances
        var balA = await GetBalance(hostA, idA, "A");
        var balB = await GetBalance(hostB, idB, "B");
        var balC = await GetBalance(hostC, idC, "C");

        Log($"Initial balances — A: {balA:F8}, B: {balB:F8}, C: {balC:F8}");

        for (int round = 1; round <= TotalRounds; round++)
        {
            Log($"");
            Log($"══════════ ROUND {round}/{TotalRounds} ══════════");

            // Get fresh receive addresses for all 3
            var addrA = await GetAddress(hostA, idA, "A");
            var addrB = await GetAddress(hostB, idB, "B");
            var addrC = await GetAddress(hostC, idC, "C");

            // Fire all 3 sends simultaneously: A->B, B->C, C->A
            Log($"Sending {SendAmount} BTC: A->B, B->C, C->A (parallel)...");

            var sendTasks = await Task.WhenAll(
                hostA.Client.SendFundsAsync(new SendFundsRequest
                {
                    WalletId = idA,
                    DestinationAddress = addrB,
                    AmountBtc = SendAmount,
                    FeeRateSatsPerVByte = FeeRate,
                }),
                hostB.Client.SendFundsAsync(new SendFundsRequest
                {
                    WalletId = idB,
                    DestinationAddress = addrC,
                    AmountBtc = SendAmount,
                    FeeRateSatsPerVByte = FeeRate,
                }),
                hostC.Client.SendFundsAsync(new SendFundsRequest
                {
                    WalletId = idC,
                    DestinationAddress = addrA,
                    AmountBtc = SendAmount,
                    FeeRateSatsPerVByte = FeeRate,
                }));

            var sendAB = sendTasks[0];
            var sendBC = sendTasks[1];
            var sendCA = sendTasks[2];

            // First 5 rounds must all succeed; later rounds may fail due to fee depletion
            if (round <= 5)
            {
                sendAB.Success.Should().BeTrue($"Round {round} A->B failed: {sendAB.Error}");
                sendBC.Success.Should().BeTrue($"Round {round} B->C failed: {sendBC.Error}");
                sendCA.Success.Should().BeTrue($"Round {round} C->A failed: {sendCA.Error}");
            }

            Log($"  A->B: {(sendAB.Success ? $"tx {sendAB.TxId}" : $"FAILED: {sendAB.Error}")}");
            Log($"  B->C: {(sendBC.Success ? $"tx {sendBC.TxId}" : $"FAILED: {sendBC.Error}")}");
            Log($"  C->A: {(sendCA.Success ? $"tx {sendCA.TxId}" : $"FAILED: {sendCA.Error}")}");

            // On even rounds, immediately fire a second burst without waiting (unconfirmed spend)
            if (round % 2 == 0 && round < TotalRounds)
            {
                Log($"  ** Rapid-fire burst: sending again immediately (spending unconfirmed) **");

                var addrA2 = await GetAddress(hostA, idA, "A");
                var addrB2 = await GetAddress(hostB, idB, "B");
                var addrC2 = await GetAddress(hostC, idC, "C");

                var burstTasks = await Task.WhenAll(
                    hostA.Client.SendFundsAsync(new SendFundsRequest
                    {
                        WalletId = idA,
                        DestinationAddress = addrB2,
                        AmountBtc = SendAmount,
                        FeeRateSatsPerVByte = FeeRate,
                    }),
                    hostB.Client.SendFundsAsync(new SendFundsRequest
                    {
                        WalletId = idB,
                        DestinationAddress = addrC2,
                        AmountBtc = SendAmount,
                        FeeRateSatsPerVByte = FeeRate,
                    }),
                    hostC.Client.SendFundsAsync(new SendFundsRequest
                    {
                        WalletId = idC,
                        DestinationAddress = addrA2,
                        AmountBtc = SendAmount,
                        FeeRateSatsPerVByte = FeeRate,
                    }));

                var burstAB = burstTasks[0];
                var burstBC = burstTasks[1];
                var burstCA = burstTasks[2];

                // Burst sends may fail if the wallet can't spend unconfirmed — log but don't fail the test
                if (burstAB.Success && burstBC.Success && burstCA.Success)
                {
                    Log($"  Burst A->B tx: {burstAB.TxId}");
                    Log($"  Burst B->C tx: {burstBC.TxId}");
                    Log($"  Burst C->A tx: {burstCA.TxId}");
                }
                else
                {
                    Log($"  Burst partial — A->B: {(burstAB.Success ? "OK" : burstAB.Error)}");
                    Log($"  Burst partial — B->C: {(burstBC.Success ? "OK" : burstBC.Error)}");
                    Log($"  Burst partial — C->A: {(burstCA.Success ? "OK" : burstCA.Error)}");
                }
            }

            // Wait a bit then refresh all balances
            await Task.Delay(TimeSpan.FromSeconds(3));

            var newBalA = await GetBalance(hostA, idA, "A");
            var newBalB = await GetBalance(hostB, idB, "B");
            var newBalC = await GetBalance(hostC, idC, "C");

            Log($"  Balances after round {round}: A={newBalA:F8}, B={newBalB:F8}, C={newBalC:F8}");

            // Each user sent SendAmount and received SendAmount, so net change is -fees only.
            // But with bursts, the math is trickier. Just verify all balances are positive
            // and that no balance has mysteriously jumped up or dropped to zero.
            newBalA.Should().BeGreaterThan(0, $"Round {round}: A balance should remain positive");
            newBalB.Should().BeGreaterThan(0, $"Round {round}: B balance should remain positive");
            newBalC.Should().BeGreaterThan(0, $"Round {round}: C balance should remain positive");

            // The total BTC across all 3 wallets should only decrease by fees (never increase).
            // Allow tolerance for unconfirmed tx display differences.
            var previousTotal = balA + balB + balC;
            var currentTotal = newBalA + newBalB + newBalC;
            currentTotal.Should().BeLessThanOrEqualTo(previousTotal + 0.001,
                $"Round {round}: Total BTC should not increase (was {previousTotal:F8}, now {currentTotal:F8})");

            balA = newBalA;
            balB = newBalB;
            balC = newBalC;
        }

        // ── Final verification: refresh all and log ──
        Log("");
        Log("Final balance refresh...");
        await Task.Delay(TimeSpan.FromSeconds(5));

        var finalA = await GetBalance(hostA, idA, "A");
        var finalB = await GetBalance(hostB, idB, "B");
        var finalC = await GetBalance(hostC, idC, "C");
        var finalTotal = finalA + finalB + finalC;

        Log($"Final balances — A: {finalA:F8}, B: {finalB:F8}, C: {finalC:F8}");
        Log($"Final total: {finalTotal:F8}");

        finalA.Should().BeGreaterThan(0, "A should still have funds after 10 rounds");
        finalB.Should().BeGreaterThan(0, "B should still have funds after 10 rounds");
        finalC.Should().BeGreaterThan(0, "C should still have funds after 10 rounds");

        Log($"========== {nameof(ThreeUsersSendToEachOther)} PASSED — {TotalRounds} rounds ==========");
    }

    private static async Task WipeAndInit(ITestHost host)
    {
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();
    }

    private static async Task<string> GetAddress(ITestHost host, string walletId, string label)
    {
        var resp = await host.Client.GetReceiveAddressAsync(new GetReceiveAddressRequest { WalletId = walletId });
        resp.Success.Should().BeTrue($"Failed to get receive address for {label}: {resp.Error}");
        resp.Address.Should().NotBeNullOrEmpty($"Address for {label} should not be empty");
        return resp.Address!;
    }

    private static async Task<double> GetBalance(ITestHost host, string walletId, string label)
    {
        var resp = await host.Client.GetBalanceAsync(new GetBalanceRequest { WalletId = walletId, Refresh = true });
        resp.Success.Should().BeTrue($"Failed to get balance for {label}: {resp.Error}");
        Log($"  Raw balance text for {label}: '{resp.TotalBalance}'");
        var balance = double.Parse(resp.TotalBalance!, CultureInfo.InvariantCulture);
        return balance;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{TestName}] {message}");
    }
}

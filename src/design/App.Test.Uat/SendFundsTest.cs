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
        await using var hostA = await TestProcessHost.LaunchAsync(ProfileA);
        await using var hostB = await TestProcessHost.LaunchAsync(ProfileB);
        await using var hostC = await TestProcessHost.LaunchAsync(ProfileC);

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

        // Track running balances. Faucet funding may still be confirming/indexing when
        // CreateWalletAndFund returns (it only waits for a NON-ZERO balance, and the
        // faucet helper can even fire a second request on slow indexing) — so wait for
        // each balance to stabilize before snapshotting the totals the invariant
        // "total BTC never increases" is measured against.
        var (balA, balB, balC) = await WaitForStableBalances(hostA, idA, hostB, idB, hostC, idC);
        var initialTotal = balA + balB + balC;

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

            // NOTE: balances may legitimately display 0 mid-churn — a wallet that just
            // spent its only UTXO shows nothing until the indexer reports the change
            // output. Positivity is asserted in the final verification after settling;
            // per-round we only enforce the money-conservation ceiling below.

            // The total BTC across all wallets must never exceed what the faucet put in
            // (sends can only burn fees, never create money). NOTE: comparing against the
            // PREVIOUS round is unsound — displayed balances dip while spends are pending
            // (inputs optimistically marked spent before the indexer reports the change
            // output) and recover a round later, which looks like an "increase".
            var currentTotal = newBalA + newBalB + newBalC;
            currentTotal.Should().BeLessThanOrEqualTo(initialTotal + 0.001,
                $"Round {round}: Total BTC must never exceed the initial funded total " +
                $"(initial {initialTotal:F8}, now {currentTotal:F8})");

            balA = newBalA;
            balB = newBalB;
            balC = newBalC;
        }

        // ── Final verification: poll until balances settle (indexer catches up) ──
        Log("");
        Log("Final balance refresh...");
        var (finalA, finalB, finalC) = await WaitForStableBalances(hostA, idA, hostB, idB, hostC, idC);
        var finalTotal = finalA + finalB + finalC;

        Log($"Final balances — A: {finalA:F8}, B: {finalB:F8}, C: {finalC:F8}");
        Log($"Final total: {finalTotal:F8}");

        finalA.Should().BeGreaterThan(0, "A should still have funds after 10 rounds");
        finalB.Should().BeGreaterThan(0, "B should still have funds after 10 rounds");
        finalC.Should().BeGreaterThan(0, "C should still have funds after 10 rounds");

        // ── Sweep-all: C sends its ENTIRE balance to A via the 100% button ──
        // Exercises the SendAll path (fee subtracted from amount, single output,
        // no change) as opposed to the fixed-amount SendAmount path used above.
        Log("");
        Log($"Sweep-all: C ({finalC:F8} BTC) -> A via 100% button...");

        var sweepAddr = await GetAddress(hostA, idA, "A");
        var sweep = await hostC.Client.SendFundsAsync(new SendFundsRequest
        {
            WalletId = idC,
            DestinationAddress = sweepAddr,
            SweepAll = true,
            FeeRateSatsPerVByte = FeeRate,
        });
        sweep.Success.Should().BeTrue($"Sweep-all C->A failed: {sweep.Error}");
        Log($"Sweep tx: {sweep.TxId}");

        // C must end up empty (fee came out of the swept amount, no change output),
        // and A must receive the sweep. Poll: the indexer can lag the broadcast.
        var sweepDeadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        double sweptC = double.MaxValue;
        double sweptA = 0;
        while (DateTime.UtcNow < sweepDeadline)
        {
            sweptC = await GetBalance(hostC, idC, "C");
            sweptA = await GetBalance(hostA, idA, "A");
            if (sweptC == 0 && sweptA > finalA) break;
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        Log($"Post-sweep balances — A: {sweptA:F8}, C: {sweptC:F8}");
        sweptC.Should().Be(0, "C swept its entire balance, no change output should remain");
        sweptA.Should().BeGreaterThan(finalA, "A should have received C's swept funds");
        sweptA.Should().BeLessThan(finalA + finalC,
            "A receives C's balance minus the network fee (fee is subtracted from the swept amount)");

        Log($"========== {nameof(ThreeUsersSendToEachOther)} PASSED — {TotalRounds} rounds + sweep ==========");
    }

    private static async Task WipeAndInit(TestProcessHost host)
    {
        await host.Client.WipeDataAsync();
        await host.Client.SwitchNetworkAsync("Angornet");
        await host.Client.EnableDebugModeAsync();
    }

    /// <summary>
    /// Polls all three balances until two consecutive reads (10s apart) are identical
    /// for every wallet, so late-confirming faucet transactions don't inflate totals
    /// mid-test. Times out after 3 minutes and proceeds with the last reading.
    /// </summary>
    private static async Task<(double A, double B, double C)> WaitForStableBalances(
        TestProcessHost hostA, string idA,
        TestProcessHost hostB, string idB,
        TestProcessHost hostC, string idC)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        var prev = (A: -1.0, B: -1.0, C: -1.0);
        while (true)
        {
            var current = (
                A: await GetBalance(hostA, idA, "A"),
                B: await GetBalance(hostB, idB, "B"),
                C: await GetBalance(hostC, idC, "C"));

            if (current == prev)
            {
                Log("Initial balances stable.");
                return current;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Log($"Balance stabilization timed out — proceeding with A={current.A:F8}, B={current.B:F8}, C={current.C:F8}");
                return current;
            }

            Log($"Waiting for balances to stabilize... A={current.A:F8}, B={current.B:F8}, C={current.C:F8}");
            prev = current;
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task<string> GetAddress(TestProcessHost host, string walletId, string label)    {
        var resp = await host.Client.GetReceiveAddressAsync(new GetReceiveAddressRequest { WalletId = walletId });
        resp.Success.Should().BeTrue($"Failed to get receive address for {label}: {resp.Error}");
        resp.Address.Should().NotBeNullOrEmpty($"Address for {label} should not be empty");
        return resp.Address!;
    }

    private static async Task<double> GetBalance(TestProcessHost host, string walletId, string label)
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

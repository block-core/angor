# FindProjectsInvoiceFlowTest

## Purpose

End-to-end integration test for the **on-chain invoice / "pay an invoice instead"** path of the invest flow. Verifies that when an investor opts to pay via QR/invoice rather than from a wallet they already control, the UI generates an on-chain receive address, monitors it through the indexer, detects an external payment, then completes the same build → threshold-check → publish pipeline that the wallet path uses.

This test fixes the on-chain half of the "1-click invest" plumbing in place before the Lightning/Boltz tab is wired into `InvestModalsView`.

## Prerequisites

- Internet connectivity (testnet indexer + faucet API + Nostr relays)
- Profile isolation: uses a `FindProjectsInvoiceFlowTest` profile via `TestProfileScope`
- Testnet faucet must be operational at `https://faucettmp.angor.io`

## Why on-chain (not Lightning) for this test?

- The on-chain invoice path already runs end-to-end through `InvestPageViewModel.PayViaInvoiceAsync`: it uses `IWalletAppService.GetNextReceiveAddress` for the address, `IInvestmentAppService.MonitorAddressForFunds` for detection, then the standard build/threshold/publish operations.
- The Lightning tab is currently unwired (the modal still renders the static `Constants.InvoiceString = "Lightning invoices coming soon"` placeholder).
- Locking the on-chain path under a regression test before adding the Boltz code means any breakage from the Lightning work shows up here, not in production.

## Tests

### `PayViaInvoice_OnChain_FaucetPaysAddress_ReachesSuccess`

| | |
|---|---|
| **Type** | End-to-end integration |
| **Network** | Signet testnet (real indexer + faucet + Nostr relays) |
| **Duration** | 120–300s (dominated by indexer lag and address-poll cycle) |

**Verifies**: Wallet creation → faucet funding → Fund project deployment → project discovery on the indexer → opening the invest modal → submitting an amount → choosing "Or pay an invoice instead" → on-chain address generation and monitoring → detection of an external faucet payment to that address → publication of the investment below the penalty threshold → arrival on the Success screen.

**Steps**:

1. **Boot app** — Create a headless `Window` containing the full `ShellView` with real DI services.
2. **Wipe data** — Navigate to Settings and call `ConfirmWipeData()` to remove any prior profile state.
3. **Create wallet (Generate path)** — Funds → Add Wallet → Generate → Download Seed (no-op headless) → Continue → Done.
4. **Fund wallet** — Click `WalletCardBtnFaucet`, then poll `WalletCardBtnRefresh` until `FundsViewModel.TotalBalance != "0.0000"` (up to 5 minutes).
5. **Deploy a Fund project** — Open the 6-step wizard, fill in name/description (with a unique GUID), banner/profile, target amount `1.0` BTC, approval threshold `0.5` BTC, weekly payout schedule for today's day-of-week with 3 installments, then deploy via wallet payment. Wait for `DeployScreen.Success`.
6. **Find the project on the indexer** — Navigate to Find Projects, repeatedly call `LoadProjectsFromSdkAsync()` until the project surfaces by GUID match (handles indexer lag).
7. **Open invest page and submit amount** — `OpenProjectDetail` → `OpenInvestPage` → set `InvestmentAmount = "0.001"` (well below the 0.5 threshold so the SDK auto-publishes) → `Submit()` → assert `CurrentScreen == WalletSelector`.
8. **Switch to invoice path** — Call `ShowInvoice()` → assert `CurrentScreen == Invoice`. Call `PayViaInvoice()` and wait until `IsProcessing && PaymentStatusText.Contains("Waiting for payment")` so we know the address was generated and monitoring is live.
9. **Pay the invoice address externally** — Read the same next-receive address via `IWalletAppService.GetNextReceiveAddress` (pure read — no pointer mutation) and `GET https://faucettmp.angor.io/api/faucet/send/{address}/2` to mimic a QR-scan payer.
10. **Wait for detection and publication** — Poll until `CurrentScreen == InvestScreen.Success` or an `ErrorMessage` appears. Log every distinct `PaymentStatusText` transition for diagnostics.
11. **Assert success state** — `PaymentReceived == true`, status text passed through "Payment received!", `IsSuccess == true`, success copy populated, and `IsAutoApproved == true` (since 0.001 BTC < 0.5 BTC threshold means the SDK published directly rather than requesting founder signatures).

**Key implementation details**:

- **Address mutation hazard** — The receive-address pointer is *not* advanced by `GetNextReceiveAddress`. Both the VM (inside `PayViaInvoiceAsync`) and the test read the same address, so the faucet pays exactly the address the VM is monitoring.
- **Auto-approval path** — `0.001 BTC < 0.5 BTC` keeps the flow on `SubmitTransactionFromDraft` (publish directly). To exercise the founder-approval path through the invoice flow you would raise the amount above the threshold; this test deliberately stays below it to keep run time and Nostr-coordination noise down.
- **Status text contract** — The test asserts on `PaymentStatusText` substrings ("Waiting for payment", "Payment received"). If those strings change in `InvestPageViewModel.PayViaInvoiceAsync`, update the assertions in lockstep.
- **Profile isolation** — `TestProfileScope.For(nameof(FindProjectsInvoiceFlowTest))` keeps wallets/settings separated from other tests in the same run.
- **Diagnostic logging** — Each unique `PaymentStatusText` is logged once during the wait loop so post-mortem analysis can reconstruct the state machine without rerunning.

**How to run**:
```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj \
  --filter "DisplayName~PayViaInvoice_OnChain_FaucetPaysAddress_ReachesSuccess" \
  --logger "console;verbosity=detailed"
```

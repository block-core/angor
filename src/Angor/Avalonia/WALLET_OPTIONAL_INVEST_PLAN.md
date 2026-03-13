# Plan: Wallet-Optional On-Chain Invest Dialog in InvestView.razor

> **⚠️ IMPORTANT: Progress Tracking**
> After completing each step below, update this file by adding `✅ COMPLETED` at the beginning of that step's heading.
> This ensures that if a crash or interruption occurs, progress is not lost and work can resume from the correct step.
> Example: `### Step 1:` becomes `### ✅ COMPLETED Step 1:`

---

## Summary

Redesign the Blazor `InvestView.razor` so clicking "Invest" opens an on-chain address/QR modal where the user sends funds externally. When no wallet exists, auto-create one (mnemonic → encrypt → save → get address). Extract address polling into a shared service in `Angor.Shared` with tests first. Show wallet backup only after investment completes or if funds arrived but the process failed.

---

## Steps

### ✅ COMPLETED Step 1: Write tests for `IAddressPollingService`

**Location:** `Angor.Sdk.Tests/Funding/Investor/Operations/AddressPollingServiceTests.cs`

Create unit tests that mock `IIndexerService.FetchUtxoAsync` and test the core polling loop behavior that currently lives inside `MempoolMonitoringService.MonitorAddressForFundsAsync`:

- **(a)** Returns UTXOs when sufficient funds found on first poll.
- **(b)** Retries with delay then succeeds on subsequent poll.
- **(c)** Returns empty list on `CancellationToken` cancellation.
- **(d)** Returns empty list on timeout.
- **(e)** Sums multiple UTXOs correctly against `requiredSats`.

These tests lock down the existing behavior before the refactor so we can prove no regression.

---

### ✅ COMPLETED Step 2: Extract `IAddressPollingService` / `AddressPollingService` into `Angor.Shared.Services`

Create a new interface `IAddressPollingService` in `Angor.Shared.Services` with a single method:

```csharp
Task<List<UtxoData>> WaitForFundsAsync(string address, long requiredSats, TimeSpan timeout, TimeSpan pollInterval, CancellationToken ct)
```

Move the polling loop (call `IIndexerService.FetchUtxoAsync`, check sum >= `requiredSats`, delay, repeat until timeout/cancel) from `MempoolMonitoringService.MonitorAddressForFundsAsync` into `AddressPollingService`.

Refactor `MempoolMonitoringService` to delegate to `IAddressPollingService`.

Run existing `MonitorAddressForFundsTests` (15+ tests) to verify no regression — the `MonitorAddressForFunds` MediatR handler still calls `IMempoolMonitoringService` which now delegates to the new service.

Register `IAddressPollingService` in `Angor.Sdk/Funding/FundingContextServices.cs`.

---

### ✅ COMPLETED Step 3: Remove the `hasWallet` gate in `InvestView.razor`

**File:** `Angor/Client/Pages/InvestView.razor`

- Remove the `@if (!hasWallet)` block (lines 47–50 in the markup).
- Remove the redirect in `OnInitializedAsync` (lines 399–402 in `@code`).
- Add a `bool walletAvailable` field set from `hasWallet`.
- Guard wallet-dependent UI behind `@if (walletAvailable)`: miner fee display (show "—" when no wallet), Angor fee display, balance-dependent logic.
- The "Continue to Confirmation" button becomes conditionally visible; when `!walletAvailable`, show "Invest" button instead that triggers the new address-based flow.

---

### ✅ COMPLETED Step 4: Add inline wallet auto-creation in the invest handler

**File:** `Angor/Client/Pages/InvestView.razor`

When `!walletAvailable` and user clicks "Invest":

1. Prompt for encryption password via `passwordComponent`.
2. Generate a 12-word mnemonic (same pattern as the `/wallet` create page — `new Mnemonic(Wordlist.English, WordCount.Twelve)`).
3. Call `_WalletOperations.BuildAccountInfoForWalletWords(walletWords)` to build account info.
4. Encrypt and persist via `storage`.
5. Set `walletAvailable = true` and `walletAutoCreated = true`.
6. Call `accountInfo.GetNextReceiveAddress()` to get the on-chain receive address.

This mirrors Avalonia's `WalletContext.GetOrCreate()`.

---

### ✅ COMPLETED Step 5: Add on-chain invoice modal in `InvestView.razor`

**File:** `Angor/Client/Pages/InvestView.razor`

Add new fields:
- `bool showInvoiceModal`
- `string invoiceAddress`
- `bool paymentReceived`
- `CancellationTokenSource invoiceMonitorCts`

Add modal markup after the existing confirmation modal that shows:
- The generated receive address with a copy button.
- QR code via the existing `ShowQRCode.razor` component from `Angor.Client.Shared`.
- Required amount in BTC.
- "Awaiting payment…" spinner that transitions to "Payment received ✓".

On modal close, call `invoiceMonitorCts.Cancel()` to auto-cancel polling.

Start with on-chain only (no Lightning tab).

---

### ✅ COMPLETED Step 6: Wire up polling → build → submit in the invoice modal

**File:** `Angor/Client/Pages/InvestView.razor`

After showing the invoice modal:

1. Call `IAddressPollingService.WaitForFundsAsync(invoiceAddress, requiredSats, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5), invoiceMonitorCts.Token)`.
2. On detecting UTXOs: set `paymentReceived = true`, update UI.
3. Build the investment transaction via `_InvestorTransactionActions.CreateInvestmentTransaction(projectInfo, investorPubKey, amount)`.
4. Sign with `_WalletOperations.AddInputsFromAddressAndSignTransaction(invoiceAddress, changeAddress, tx, walletWords, accountInfo, feeRate)`.
5. Follow the existing `Send()` flow — strip witness, derive Nostr key, call `_SignService.RequestInvestmentSigs(...)`, store `InvestorProject`, listen for founder signatures.

---

### ✅ COMPLETED Step 7: Add payment method choice when wallet has balance

**File:** `Angor/Client/Pages/InvestView.razor`

When `walletAvailable` and `WalletBalance >= investmentAmount`:
- Show a choice modal with two options:
  - **"Pay with Wallet"** — existing `InvestFunds()` → `showCreateModal` flow.
  - **"Send to Address"** — new `showInvoiceModal` flow.

When wallet exists but balance is insufficient, go directly to `showInvoiceModal`.

This mirrors Avalonia's `PaymentSelectorViewModel`.

---

### ✅ COMPLETED Step 8: Show wallet backup after investment or on partial failure

**File:** `Angor/Client/Pages/InvestView.razor`

Track:
- `bool walletAutoCreated` — set in Step 4 when wallet is created on the fly.
- `bool fundsReceivedOnNewWallet` — set to `true` when `walletAutoCreated && WaitForFundsAsync` returns UTXOs.

Show backup modal with seed words (via `passwordComponent.GetWalletAsync()`) and "Download Seed" + "Continue" buttons when:
- Investment succeeds, OR
- `fundsReceivedOnNewWallet` is true but investment build/submit failed.

Do **NOT** show the backup prompt before funds are sent.

---

## Known Issues (to fix separately)

- **`AddInputsFromAddressAndSignTransaction` testnet bug** — Known pre-existing bug with `"Mismatching human readable part"` errors on testnet/signet. Will be fixed as a separate follow-up task after this change lands.

## Design Decisions

- **Cancellation on modal close** — When the user closes the invoice modal while polling is active, cancel via `CancellationTokenSource.Cancel()`. Spinner + auto-cancel on close is sufficient UX, no countdown timer needed.
- **Tests first** — Every extraction of logic to `Angor.Shared` must have tests proving no regression before the refactor.
- **On-chain only** — Start with on-chain payments only. Lightning can be added later as a second invoice type tab.




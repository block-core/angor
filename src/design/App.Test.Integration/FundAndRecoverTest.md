# FundAndRecoverTest

## Purpose

Full end-to-end integration test that boots the real Avalonia app in headless mode, creates a wallet, funds it via a testnet faucet, creates a **Fund-type** project through the 6-step wizard, invests in that project above the approval threshold, has the **founder approve the pending request**, has the **investor confirm the signed investment**, then has the **founder spend stage 1**, and finally has the **investor recover the remaining stages** — verifying the complete approval-spend-recover lifecycle through the UI and SDK.

## Prerequisites

- Internet connectivity (testnet indexer + faucet API + Nostr relays)
- Profile isolation: uses `test-send-receive` profile (`~/.local/share/App/Profiles/test-send-receive/`)
- Testnet faucet must be operational at `https://faucettmp.angor.io`

## Why Fund (not Invest)?

- **Fund** projects use dynamic stage patterns with a penalty threshold that controls whether investments are auto-published or routed through founder approval.
- **Invest** projects have fixed stages and no penalty threshold — the concept doesn't apply.
- This test validates the penalty threshold fix: `CreateProjectViewModel.BuildCreateProjectDto()` now correctly maps `ApprovalThreshold` to `PenaltyThreshold` for Fund projects.

## Tests

### `FullFundAndRecoverFlow`

| | |
|---|---|
| **Type** | End-to-end integration |
| **Network** | Signet testnet (real indexer + faucet + Nostr relays) |
| **Duration** | 120–300s |

**Verifies**: The complete fund lifecycle — wallet setup, funding, Fund project creation via the 6-step wizard (with dynamic payout schedule), above-threshold investing through founder approval, investor confirmation after valid founder signatures, founder spending stage 1 via `ManageProjectViewModel`, and investor recovery via `PortfolioViewModel`.

**Steps**:

1. **Boot app** — Create a headless `Window` containing the full `ShellView` with real DI services.
2. **Wipe data** — Navigate to Settings and call `ConfirmWipeData()` to delete any wallets/settings from a previous run.
3. **Create wallet (Generate path)** — Navigate to Funds, click "Add Wallet" -> "Generate New" -> "Download Seed" (no-op in headless) -> "Continue" -> wait for success -> "Done".
4. **Fund wallet** — Request testnet coins via `GetTestCoinsAsync`, poll `WalletCardBtnRefresh` until `TotalBalance != "0.0000"` (up to 5 minutes with faucet retry).
5. **Create Fund project** — Navigate to My Projects, open the 6-step wizard:
   - Step 1: Dismiss welcome, select **"fund"** type
   - Step 2: Set project name (with unique GUID) and description
   - Step 3: Set random picsum.photos banner and profile image URLs
   - Step 4: Set goal amount (1 BTC), approval threshold stays at default "0.001" BTC (will be set to "0.01" in wizard for test margin)
   - Step 5: Dismiss welcome, set `PayoutFrequency="Weekly"`, select installment count `[3]`, set `WeeklyPayoutDay` to today's day of week, call `GeneratePayoutSchedule()`
   - Step 6: Deploy via wallet payment (set password provider, select wallet, pay, wait for success)
6. **Verify project deployed** — Assert project appears in My Projects list by unique GUID in description.
7. **Reload founder projects from SDK** — Call `LoadFounderProjectsAsync()` to get the `ProjectIdentifier` and `OwnerWalletId` (not set by `OnProjectDeployed`).
8. **Navigate to Find Projects** — Reload projects from SDK via `LoadProjectsFromSdkAsync()`, find our project by GUID match.
9. **Invest 0.02 BTC** — Open invest page, set amount to "0.02" BTC (above the 0.01 BTC approval threshold), submit, select wallet, pay. The SDK pipeline: `BuildInvestmentDraft` -> `CheckPenaltyThreshold` (returns true for above-threshold) -> `SubmitInvestment` (request founder signatures).
10. **Add investment to portfolio** — Call `AddToPortfolio()` on the invest page, reload portfolio from SDK, and verify there is exactly one entry for the project (no duplicate optimistic + SDK entries).
11. **Founder approves request** — Navigate to Funders, poll `LoadInvestmentRequestsAsync()` until the request appears in `waiting`, then call `ApproveSignature(...)` and verify it moves to `approved`.
12. **Investor confirms signed investment** — Reload funded investments from SDK until the investment reaches `FounderSignaturesReceived`/step 2, then call `ConfirmInvestmentAsync(...)` and verify it advances to active/step 3.
13. **Founder spends stage 1** — Via `ManageProjectViewModel`:
    - `OpenManageProject(project)` creates the manage VM
    - `LoadClaimableTransactionsAsync()` loads stages with UTXOs
    - Verify `Stages.Count == 3`, stage numbers are sequential, every stage has non-zero amount/UTXO data, and stage 1 is the spendable stage before claim
    - Wait for stage 1 to have available transactions (indexer lag)
    - Select all available transactions for stage 1
    - `ClaimStageFundsAsync(1, selectedTxs, feeRate)` builds and broadcasts the spending tx
14. **Investor recovers remaining stages** — Via `PortfolioViewModel`:
    - Reload investments from SDK
    - Find our investment
    - `LoadRecoveryStatusAsync(investment)` — poll until recovery action available
    - Verify the recovery-stage VM list is populated, stage 1 is `Released`, and the remaining stages are still unreleased
    - Execute the appropriate recovery action based on `RecoveryState.ActionKey`
15. **Verify recovery succeeded** — Assert the recovery operation returned `true`.

**Key implementation details**:

- **Fund vs Invest wizard flow**: Fund projects use `PayoutFrequency`, `SelectedInstallmentCounts`, `WeeklyPayoutDay`, and `GeneratePayoutSchedule()` instead of `DurationValue`/`DurationUnit`/`ReleaseFrequency`/`GenerateInvestmentStages()`.
- **Approval threshold**: Set `ApprovalThreshold = "0.01"` (0.01 BTC) in the wizard. The investment of 0.02 BTC is above this threshold, so `CheckPenaltyThreshold` returns `IsAboveThreshold=true` and the SDK routes the flow through founder signatures.
- **Signature validation**: The investor-side signature validation happens in the SDK during `ConfirmInvestment`, not in the design app UI. `PublishInvestment.ValidateFounderSignatures(...)` validates the founder signatures before the signed transaction is broadcast.
- **Payout day = today**: `WeeklyPayoutDay` is set to today's day of week so stage 1 is immediately claimable by the founder under the SDK's weekly payout calculation.
- **`OnProjectDeployed` gap**: The `OnProjectDeployed()` callback adds a `MyProjectItemViewModel` without `ProjectIdentifier` or `OwnerWalletId`. The test calls `LoadFounderProjectsAsync()` to reload from SDK and get these fields.
- **Pattern index**: For Fund projects, `InvestPageViewModel` uses `patternIndex = 0` (first pattern) when building the investment draft.
- **Password provider**: Must set `SimplePasswordProvider` to `"default-key"` before deploy/invest/spend steps.
- **Deploy callback wiring**: The test manually wires `OnProjectDeployed` (normally done by `MyProjectsView.OpenCreateWizard` code-behind).
- **PortfolioViewModel is singleton**: The same instance is shared across the invest page (for `AddToPortfolio`) and the funded section.
- **Extra VM assertions**: The test now also checks `ProjectType`, `TypeLabel`, `StatusClass`, `ShowRecoverButton`, `StagesToRecover`, `AmountToRecover`, `ButtonMode`, and pre-spend stage header stats to catch UI-model regressions earlier.

**How to run**:
```bash
dotnet test src/design/App.Test.Integration/App.Test.Integration.csproj \
  --filter "DisplayName~FullFundAndRecoverFlow" \
  --logger "console;verbosity=detailed"
```

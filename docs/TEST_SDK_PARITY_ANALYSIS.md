# SDK Call Parity: Analysis of What Was Done and What Remains

This document analyzes the 9 critical SDK call parity gaps identified in the
[SDK Call Comparison](../sdk-call-comparison-app-vs-avalonia.md) between the design app
(`src/design/App/`) and the Avalonia reference app (`src/avalonia/AngorApp/`). For each gap,
we assess whether it has been resolved, what integration test coverage exists, and what
work remains.

---

## Status Summary

| # | Gap | Status | Integration Test Coverage |
|---|-----|--------|--------------------------|
| 1 | ConfirmInvestment | **DONE** | Covered (3 tests) |
| 2 | CancelInvestmentRequest | **DONE** | Not covered |
| 3 | INetworkConfiguration.SetNetwork() | **DONE** | Not covered |
| 4 | Lightning payments | **NOT DONE** | Not covered |
| 5 | Recovery state machine | **DONE** | Partial |
| 6 | Transaction draft preview | **NOT DONE** | Not covered |
| 7 | BuildInvestmentDraft FundingAddress | **DONE** | Not covered |
| 8 | Fee rates hardcoded to 20 | **PARTIAL** | Not covered |
| 9 | DeleteAllDataAsync on wipe/switch | **DONE** | Not covered |

**7 of 9 gaps resolved. 2 remain open. Only 1 of the fixes has integration test coverage.**

---

## Detailed Analysis

### Gap 1: ConfirmInvestment -- DONE

**What was missing:** No way to publish an investment after the founder signs it.

**What was implemented:**
- `PortfolioViewModel.ConfirmInvestmentAsync()` at `src/design/App/UI/Sections/Portfolio/PortfolioViewModel.cs:941`
- Calls `IInvestmentAppService.ConfirmInvestment(PublishInvestmentRequest)` with the investment transaction hex, project identifier, and wallet ID.
- UI button "Confirm Investment" in `InvestmentDetailView.axaml:462`.
- Code-behind handler in `InvestmentDetailView.axaml.cs:37`.

**Integration test coverage:** Covered by 3 tests:
- `FundAndRecoverTest` (line 504)
- `MultiFundClaimAndRecoverTest` (line 493)
- `MultiInvestClaimAndRecoverTest` (line 497)

**Remaining work:** None. This gap is fully resolved and tested.

---

### Gap 2: CancelInvestmentRequest -- DONE

**What was missing:** No way to cancel a pending investment.

**What was implemented:**
- `PortfolioViewModel.CancelInvestmentAsync()` at `PortfolioViewModel.cs:993`
- Calls `IInvestmentAppService.CancelInvestmentRequest(CancelInvestmentRequestRequest)`.
- UI buttons in `InvestmentDetailView.axaml:384` (step 1) and `:488` (general).
- Code-behind handler in `InvestmentDetailView.axaml.cs:41-43`.

**Integration test coverage:** None. No E2E test exercises the cancellation flow.

**Remaining work:**
- Create `InvestmentCancellationTest` (see [TEST_NEW_PROPOSALS.md](TEST_NEW_PROPOSALS.md) section 6).
- Test should verify: handshake status becomes Cancelled, funds are not locked, re-investing after cancel works.

---

### Gap 3: INetworkConfiguration.SetNetwork() -- DONE

**What was missing:** Network switching was cosmetic only -- the runtime network object was not updated.

**What was implemented:**
- `SettingsViewModel.cs:236` calls `_networkStorage.SetNetwork(newNetwork)`.
- `SettingsViewModel.cs:242` calls `_networkConfig.SetNetwork(...)` mapping string to `Network` object.
- This means the runtime `INetworkConfiguration` is now updated along with the persisted setting.

**Integration test coverage:** None. No test switches networks.

**Remaining work:**
- Create `NetworkSwitchTest` (see [TEST_NEW_PROPOSALS.md](TEST_NEW_PROPOSALS.md) section 10).
- Test should verify: DerivedProjectKeys re-derived, projects filtered by network, switch is reversible.

---

### Gap 4: Lightning Payments -- NOT DONE

**What was missing:** `CreateLightningSwap` and `MonitorLightningSwap` entirely absent.

**Current state:**
- Only UI placeholders exist: `Constants.cs:18` has `InvoiceString = "Lightning invoices coming soon"`.
- Lightning-styled tabs appear in `InvestModalsView.axaml:267` and `DeployFlowOverlay.axaml:285` but are non-functional.
- The "invoice" flow in the invest page is actually on-chain address monitoring, not Lightning.
- No Boltz swap integration code exists in the design app.

**Integration test coverage:** None (the SDK has skipped `LightningIntegrationTests` requiring a local Boltz server).

**Remaining work:**
- Implement `CreateLightningSwap` and `MonitorLightningSwap` calls in the invest flow.
- Wire up the `BoltzSwapStorageService` for swap state persistence.
- Add WebSocket monitoring for swap status updates.
- Create integration tests once implementation exists.

---

### Gap 5: Recovery State Machine -- DONE

**What was missing:** App only checked `HasItemsInPenalty` and `HasUnspentItems`. Missing `HasSpendableItemsInPenalty`, `HasReleaseSignatures`, `EndOfProject`, and `IsAboveThreshold`.

**What was implemented:**
- Full `RecoveryStatus` record at `PortfolioViewModel.cs:26-30`:
  ```
  record RecoveryStatus(HasUnspentItems, HasSpendableItemsInPenalty,
                         HasReleaseSignatures, EndOfProject, IsAboveThreshold)
  ```
- Pattern matching for all recovery paths at lines 39-54:
  - `HasReleaseSignatures` -> "Recover without Penalty" (unfunded release)
  - `EndOfProject` or `!IsAboveThreshold` -> "Recover" (end of project claim)
  - `!HasSpendableItemsInPenalty` -> "Recover to Penalty"
  - `HasSpendableItemsInPenalty` -> "Recover from Penalty" (penalty release)
- Dedicated methods:
  - `RecoverFundsAsync` (line 744) -- builds recovery transaction
  - `ReleaseFundsAsync` (line 793) -- builds unfunded release transaction
  - `ClaimEndOfProjectAsync` (line 842) -- builds end-of-project claim
  - `PenaltyReleaseFundsAsync` (line 891) -- builds penalty release transaction

**Integration test coverage:** Partial.
- `FundAndRecoverTest` covers the basic recovery path.
- `MultiFundClaimAndRecoverTest` covers penalty recovery (with PenaltyDays=0).
- `MultiInvestClaimAndRecoverTest` covers end-of-project claim.
- **Not covered:** Unfunded release path (`HasReleaseSignatures`), penalty release with non-zero penalty days.

**Remaining work:**
- Create `PenaltyRecoveryWithTimeLockTest` (see [TEST_NEW_PROPOSALS.md](TEST_NEW_PROPOSALS.md) section 9).
- Create a test for the unfunded release path (founder releases signatures, investor claims without penalty).

---

### Gap 6: Transaction Draft Preview -- NOT DONE

**What was missing:** No fee preview before broadcasting transactions anywhere in the app.

**Current state:**
- `IWalletAppService.EstimateFeeAndSize()` is never called in the design app.
- `ITransactionDraftPreviewer` / `PreviewAndCommit` are never referenced.
- All transaction flows (deploy, claim, recovery, send) broadcast directly without showing the user a fee estimate.
- The Avalonia reference app uses a two-step flow: estimate -> preview -> confirm.

**Integration test coverage:** None.

**Remaining work:**
- Add `EstimateFeeAndSize` call before `SendAmount` in `FundsViewModel`.
- Add a preview step to the deploy flow in `DeployFlowViewModel`.
- Add a preview step to claim/recovery flows in `PortfolioViewModel` and `ManageProjectViewModel`.
- Once implemented, integration tests should verify the two-step flow works and the estimated fee matches (approximately) the actual fee charged.

---

### Gap 7: BuildInvestmentDraft FundingAddress -- DONE

**What was missing:** `FundingAddress` not passed to `BuildInvestmentDraft`, causing wrong UTXO selection.

**What was implemented:**
- `InvestPageViewModel.cs:761` now passes `FundingAddress: addressResult.Value.Value` to the `BuildInvestmentDraft` request.
- The funding address is obtained from `IWalletAppService.GetNextReceiveAddress()` earlier in the flow.

**Integration test coverage:** Not directly covered. The E2E invest tests call the invest flow through the UI but don't assert that `FundingAddress` is correctly propagated.

**Remaining work:**
- The existing invest integration tests (`FundAndRecoverTest`, `MultiFundClaimAndRecoverTest`) implicitly validate this works (investments succeed), but a specific assertion that the correct address was used would strengthen confidence.

---

### Gap 8: Fee Rates Hardcoded to 20 -- PARTIAL

**What was missing:** All fee rates hardcoded to `20` sats/vbyte instead of being user-configurable.

**Current state:**
- `InvestPageViewModel.cs:113`: `[Reactive] private long selectedFeeRate = 20;` -- **default is 20 but reactive/configurable**.
- `DeployFlowViewModel.cs:46`: `[Reactive] private long selectedFeeRate = 20;` -- **same pattern**.
- `PortfolioViewModel.cs:744,793,842,891`: All recovery/claim methods take `feeRateSatsPerVByte = 20` as default parameter.
- `ManageProjectViewModel.cs:375`: `ClaimStageFundsAsync` takes `feeRateSatsPerVByte = 20` as default.
- `FundsViewModel.cs:228`: Uses `DomainFeeRate(feeRateSatsPerVByte)` -- the value comes from the UI.

**Assessment:** The fee rate is now a `[Reactive]` property in the invest and deploy flows (the UI can change it), but the **default is still 20** everywhere. The Avalonia app uses `2` sats/vbyte and has a dynamic fee selector UI. The design app has fee rate inputs in the modals but the default starting point is 10x higher than Avalonia's.

**Integration test coverage:** None. No test verifies fee rate behavior.

**Remaining work:**
- Consider lowering the default fee rate from 20 to 2 sats/vbyte to match Avalonia.
- Verify the fee rate UI inputs actually propagate to the SDK calls (integration test).
- Add `EstimateFeeAndSize` integration to give users data-driven fee selection (overlaps with Gap 6).

---

### Gap 9: DeleteAllDataAsync on Wipe/Network Switch -- DONE

**What was missing:** Incomplete data cleanup when wiping data or switching networks.

**What was implemented:**
- `SettingsViewModel.cs:28` injects `IDatabaseManagementService`.
- `SettingsViewModel.cs:227` calls `_databaseManagementService.DeleteAllDataAsync()` during network switch.
- `SettingsViewModel.cs:363` calls `_databaseManagementService.DeleteAllDataAsync()` during data wipe.
- Both paths wipe all 8 LiteDB collections.

**Integration test coverage:** None. No test exercises the wipe/switch flow.

**Remaining work:**
- Create `DatabaseIntegrityTest` (see [TEST_NEW_PROPOSALS.md](TEST_NEW_PROPOSALS.md) section 4) that calls `DeleteAllDataAsync` and verifies all collections are empty.
- Create `NetworkSwitchTest` (see [TEST_NEW_PROPOSALS.md](TEST_NEW_PROPOSALS.md) section 10) that verifies `DeleteAllDataAsync` is called during network switch and data is rebuilt.

---

## Integration Test Coverage of Resolved Gaps

Of the 7 resolved gaps, only **Gap 1 (ConfirmInvestment)** has E2E integration test coverage. The remaining 6 resolved gaps are functional in the code but untested end-to-end.

| Resolved Gap | Has Integration Test? | Proposed Test |
|---|---|---|
| 1. ConfirmInvestment | Yes (3 tests) | -- |
| 2. CancelInvestmentRequest | **No** | `InvestmentCancellationTest` |
| 3. SetNetwork | **No** | `NetworkSwitchTest` |
| 5. Recovery state machine | Partial (3 paths of 4) | `PenaltyRecoveryWithTimeLockTest` |
| 7. FundingAddress | Implicit only | Strengthen existing invest tests |
| 8. Fee rates (partial) | **No** | Fee rate propagation test |
| 9. DeleteAllDataAsync | **No** | `DatabaseIntegrityTest` |

---

## Recommended Next Steps

### Immediate (complete the remaining gaps)

1. **Lightning payments (Gap 4):** Implement `CreateLightningSwap` and `MonitorLightningSwap` in the invest flow. This is the largest remaining piece of work.

2. **Transaction draft preview (Gap 6):** Add `EstimateFeeAndSize` calls before all broadcast operations. Add a confirmation modal showing estimated fee and transaction size.

### Short-term (test the resolved gaps)

3. **Create `InvestmentCancellationTest`** to cover Gap 2 end-to-end.
4. **Create `NetworkSwitchTest`** to cover Gaps 3 and 9 end-to-end.
5. **Create `PenaltyRecoveryWithTimeLockTest`** to cover the remaining Gap 5 path.
6. **Lower default fee rate** from 20 to 2 sats/vbyte across all ViewModels.

### Medium-term (strengthen existing tests)

7. Add database assertions to all existing integration tests (see [TEST_IMPROVEMENTS.md](TEST_IMPROVEMENTS.md)).
8. Create SDK-level tests for database round-trips (see [TEST_SDK_PROPOSALS.md](TEST_SDK_PROPOSALS.md)).

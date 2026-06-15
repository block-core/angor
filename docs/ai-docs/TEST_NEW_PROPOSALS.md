# New Integration Test Proposals

> **Last updated:** April 2026 -- re-analyzed after new E2E tests added.

This document describes new end-to-end integration tests to create in `src/design/App.Test.Integration/`.

**Status: 2 of 10 proposals have been implemented.** The remaining 8 are still recommended.

---

## 1. ~~Wallet Import and Project Scan~~ -- IMPLEMENTED

**Status:** **DONE** -- `WalletImportAndProjectScanTest.cs` added.

**What it covers:**
- Profile A: create wallet, fund, create project, deploy
- Profile B: import same seed words
- Asserts: same `WalletId` (deterministic derivation), non-zero balance (UTXO discovery), `ScanFounderProjects` finds the project, project name and identifier match

**Remaining gaps in the implementation:**
- Does not compare `DerivedProjectKeys` between profiles (proposed in TEST_IMPROVEMENTS.md section 6.1)
- Does not verify `InvestmentRecordsDocument` discoverable after import

---

## 2. Wallet Delete and Reimport (HIGH PRIORITY)

**Status:** NOT DONE

**Why:** Delete has a known gap (Bug #1): `DerivedProjectKeys`, `FounderProjectsDocument`, `InvestmentRecordsDocument`, and `InvestmentHandshake` are NOT cleaned up. This test documents and exposes that behavior.

**File:** `WalletDeleteAndReimportTest.cs`

### Steps

1. Create wallet, fund it, create a project.
2. Record seed words and wallet ID.
3. Delete the wallet via the UI.
4. Assert: `EncryptedWallet` removed from `wallets.json`.
5. Assert: `WalletAccountBalanceInfo` removed from LiteDB.
6. Assert (document the gap): `DerivedProjectKeys` still exists in DB.
7. Assert (document the gap): `FounderProjectsDocument` still exists in DB.
8. Assert: UI shows no wallets.
9. Import wallet with the same seed words.
10. Assert: Wallet recreated with the same `WalletId`.
11. Assert: Balance is recoverable from on-chain data.
12. Trigger project scan.
13. Assert: Project is rediscovered and visible.

### What This Validates

- The delete flow and exactly what gets cleaned up.
- Documents the orphaned data issue for future fix.
- Reimport after delete restores full functionality.

---

## 3. Many Investors Scenario (HIGH PRIORITY)

**Status:** NOT DONE

**Why:** Current tests use 2-3 investors max. Real projects may have many investors, and the threshold/approval logic becomes complex at scale.

**File:** `ManyInvestorsTest.cs`

### Steps

1. Founder creates a project with `PenaltyThreshold = X satoshis`.
2. **Investors 1-3:** Invest below threshold (should be auto-approved).
3. **Investors 4-5:** Invest above threshold (require founder approval).
4. Assert: Founder sees all 5 handshake requests in the UI.
5. Assert: `InvestmentHandshake` documents for investors 1-3 have `IsDirectInvestment = true`.
6. Founder approves investors 4 and 5.
7. All 5 investors confirm their investments.
8. Assert: All 5 `InvestmentHandshake` records have `Status = Invested`.
9. Founder claims stage 1.
10. Assert: Each investor can independently recover their remaining funds.
11. Assert: Total invested across all records equals the sum of individual investments.
12. Assert: All 5 `InvestmentRecordsDocument` entries exist with correct amounts.

### What This Validates

- Threshold logic works correctly at boundaries.
- Auto-approval vs manual approval paths.
- Multiple concurrent investments don't interfere.
- Founder can manage many handshakes.
- Independent recovery for each investor.

---

## 4. Database Integrity After Operations (HIGH PRIORITY)

**Status:** NOT DONE

**Why:** No test currently validates the full database lifecycle including wipe and rebuild.

**File:** `DatabaseIntegrityTest.cs`

### Steps

1. Create wallet.
   - Assert: `WalletAccountBalanceInfo` exists in LiteDB.
   - Assert: `DerivedProjectKeys` exists with 15 key slots.
   - Assert: `EncryptedWallet` exists in `wallets.json`.
2. Fund wallet.
   - Assert: `WalletAccountBalanceInfo.TotalBalance > 0` in DB.
3. Create and deploy project.
   - Assert: `FounderProjectsDocument` exists with project record.
   - Assert: `Project` exists in cache collection.
4. Invest in the project (using a second profile).
   - Assert: `InvestmentHandshake` document created.
   - Assert: `InvestmentRecordsDocument` created for investor.
5. Call `DatabaseManagementService.DeleteAllDataAsync()`.
   - Assert: All 8 collections are empty.
   - Assert: `wallets.json` is NOT affected (separate storage).
6. Trigger `RebuildAllWalletBalancesAsync`.
   - Assert: `WalletAccountBalanceInfo` rebuilt from on-chain data.
   - Assert: `DerivedProjectKeys` re-derived.
7. Trigger `ScanFounderProjects`.
   - Assert: `FounderProjectsDocument` rebuilt from indexer.

### What This Validates

- Every database write operation during the core flows.
- `DeleteAllDataAsync` correctly wipes all collections.
- `RebuildAllWalletBalancesAsync` correctly reconstructs wallet state.
- The system can fully recover from a database wipe.

---

## 5. Subscribe Project Type (MEDIUM PRIORITY)

**Status:** NOT DONE (unit tests exist for dynamic stage patterns but no E2E)

**Why:** Only `Invest` and `Fund` types are tested. `Subscribe` is completely untested E2E.

**File:** `SubscribeProjectTest.cs`

### Steps

1. Create a Subscribe-type project with `DynamicStagePatterns`.
2. Deploy.
3. Assert: `ProjectDto.ProjectType == Subscribe`.
4. Assert: `DynamicStagePatterns` correctly persisted in both Nostr and DB.
5. Investor invests in the project.
6. Founder claims first dynamic stage.
7. Assert: Stage release dates calculated correctly per the dynamic pattern.
8. Assert: Claimed amount matches the stage percentage.

### What This Validates

- Subscribe project creation wizard works E2E.
- `DynamicStagePattern` data round-trips through Nostr and DB correctly.
- Dynamic stage release date calculations work in practice.

---

## 6. ~~Investment Cancellation Flow~~ -- IMPLEMENTED

**Status:** **DONE** -- `InvestmentCancellationTest.cs` added.

**What it covers (8 phases, ~40+ assertions):**
1. Founder creates project, investor invests
2. Investor cancels **before** founder approval -- status becomes "Cancelled", balance released
3. Investor re-invests after cancel -- new handshake created
4. Founder approves the re-investment
5. Investor cancels **after** founder approval -- status becomes "Cancelled", balance released
6. Investor re-invests again
7. Founder approves again
8. Investor confirms approved investment -- reaches Step 3 (active)

**Remaining gaps in the implementation:**
- Does not verify founder-side cancellation visibility (proposed in TEST_IMPROVEMENTS.md section 7.1)
- Does not verify Nostr DM cancellation notification E2E

---

## 7. Project Discovery and Cache Behavior (MEDIUM PRIORITY)

**Status:** NOT DONE

**Why:** The `DocumentProjectService` caching layer (LiteDB cache-aside) is untested end-to-end.

**File:** `ProjectDiscoveryCacheTest.cs`

### Steps

1. Founder creates and deploys a project.
2. Switch to investor profile.
3. Browse "Find Projects" -- project appears.
4. Assert: `Project` document cached in LiteDB (check DB directly).
5. Call `DatabaseManagementService.DeleteAllDataAsync()` (wipe cache).
6. Assert: `Project` collection is empty.
7. Browse "Find Projects" again.
8. Assert: Project re-fetched from indexer/Nostr and visible.
9. Assert: `Project` document re-cached in LiteDB with same data.

### What This Validates

- Cache-aside pattern: first access fetches from network, subsequent from DB.
- Cache wipe forces re-fetch.
- Re-fetched data matches original.
- Nostr -> indexer -> DB pipeline works correctly.

---

## 8. Duplicate Wallet Import (EDGE CASE)

**Status:** NOT DONE (Bug #3 still exists)

**Why:** There is NO duplicate wallet check in `WalletFactory`. Importing the same seed twice produces the same `WalletId`, and `wallets.json` gets a duplicate entry. This edge case is currently unhandled.

**File:** `DuplicateWalletImportTest.cs`

### Steps

1. Create wallet with seed words using "Generate."
2. Record the seed words and wallet ID.
3. Import wallet using the exact same seed words.
4. Assert: What happens?
   - Does `wallets.json` now have two entries with the same ID?
   - Does the UI show one wallet or two?
   - Do balance lookups still work?
5. Document the actual behavior (this test serves as a spec).

### Expected Outcome

This test should document whether the system:
- Silently creates a duplicate (bug -- this is the current behavior per analysis).
- Detects the duplicate and rejects it (ideal).
- Crashes or corrupts data (critical bug).

The test result will drive a fix: adding a duplicate `WalletId` check to `WalletFactory.CreateWallet`.

---

## 9. Non-Zero Penalty Recovery (EDGE CASE)

**Status:** NOT DONE

**Why:** `MultiFundClaimAndRecoverTest` uses `PenaltyDays=0`. The actual penalty calculation with time-locked funds is untested.

**File:** `PenaltyRecoveryWithTimeLockTest.cs`

### Steps

1. Founder creates a Fund project with `PenaltyDays = 30`.
2. Stages set with release dates in the future.
3. Investor invests above threshold.
4. Founder approves, investor confirms.
5. Founder claims stage 1.
6. Investor recovers remaining funds (with penalty).
7. Assert: Recovered amount is LESS than `invested - stage1` (penalty was deducted).
8. Assert: Penalty amount correlates to remaining lock time.
9. Assert: `RecoveryTransactionId` stored in `InvestmentRecordsDocument`.

### What This Validates

- Penalty calculation produces a non-trivial deduction.
- Time-lock penalty is proportional to remaining duration.
- Recovery transaction includes the penalty output.

---

## 10. Wallet Network Switch (EDGE CASE)

**Status:** NOT DONE

**Why:** `RebuildAllWalletBalancesAsync` re-derives keys for the new network but this is never tested.

**File:** `NetworkSwitchTest.cs`

### Steps

1. Create wallet on Testnet, derive keys, create a project.
2. Switch network to Mainnet (or signet).
3. Assert: `DerivedProjectKeys` re-derived with the new network's `AngorKey`.
4. Assert: Old project NOT visible in "My Projects" (different network name filter).
5. Switch back to the original network.
6. Assert: Project visible again in "My Projects."
7. Assert: Balance restored.

### What This Validates

- Key re-derivation happens on network switch.
- Projects are filtered by network name.
- Network switch is reversible without data loss.

---

## Summary

| # | Proposal | Priority | Status |
|---|---|---|---|
| 1 | Wallet Import and Project Scan | HIGH | **DONE** |
| 2 | Wallet Delete and Reimport | HIGH | Not done |
| 3 | Many Investors | HIGH | Not done |
| 4 | Database Integrity | HIGH | Not done |
| 5 | Subscribe Project Type | MEDIUM | Not done |
| 6 | Investment Cancellation | MEDIUM | **DONE** |
| 7 | Project Discovery Cache | MEDIUM | Not done |
| 8 | Duplicate Wallet Import | EDGE | Not done |
| 9 | Non-Zero Penalty Recovery | EDGE | Not done |
| 10 | Network Switch | EDGE | Not done |

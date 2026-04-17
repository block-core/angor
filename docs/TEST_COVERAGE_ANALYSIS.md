# Test Coverage Analysis

> **Last updated:** April 2026 -- re-analyzed after significant test additions.

## Overview

This document provides a comprehensive analysis of the current test suite across all four test projects, identifies gaps in coverage, and recommends improvements to existing tests and new tests to create.

| Test Project | Tests (Original) | Tests (Current) | Nature |
|---|---|---|---|
| `App.Test.Integration` (src/design/) | 7 | **9** | E2E headless UI tests (real testnet) |
| `Angor.Sdk.Tests` (src/sdk/) | ~121 | **~310+** | SDK unit tests + skipped integration tests |
| `Angor.Shared.Tests` (src/shared/) | ~114 | ~105+ | Bitcoin protocol & domain model unit tests |
| `AngorApp.Tests` (src/avalonia/) | 52 | ~50+ | ViewModel & UI logic unit tests |
| **Total** | **~294** | **~475+** | |

### What Changed

The SDK test project **nearly tripled** in size since the original analysis. Two new E2E integration tests were added. Key additions:

- **~190 new SDK unit tests** covering nearly every MediatR handler (founder, investor, project, lightning operations)
- **`WalletImportAndProjectScanTest`** (E2E) -- validates wallet import from mnemonic and project scanning
- **`InvestmentCancellationTest`** (E2E) -- validates cancel before/after approval and re-investment
- **`ScanFounderProjectsTests`** (7 unit tests) -- covers storage errors, null keys, discovery, graceful degradation
- **`CancelInvestmentRequestTests`** (5 unit tests) -- covers cancel when not on-chain, already published, hash mismatch
- **`LiteDbGenericDocumentCollectionTests`** (3 unit tests) -- validates `??=` expression caching bug fix
- **Lightning/Boltz tests** (17+ unit tests) -- `ClaimLightningSwapTests`, `CreateLightningSwapTests`, `MonitorLightningSwapTests`

## Related Documents

- [Improvements to Existing Tests](TEST_IMPROVEMENTS.md)
- [New Test Proposals](TEST_NEW_PROPOSALS.md)
- [Edge Cases and Known Issues](TEST_EDGE_CASES.md)
- [SDK-Level Test Proposals](TEST_SDK_PROPOSALS.md)
- [SDK Call Parity Analysis](TEST_SDK_PARITY_ANALYSIS.md)

---

## Current Coverage by Area

### E2E Integration Tests (App.Test.Integration)

| Test File | What It Covers | Status |
|---|---|---|
| `SmokeTest.cs` | Headless platform boot, AutomationId lookup | Existing |
| `SendToSelfTest.cs` | Wallet create, faucet fund, send to self, verify balance | Existing |
| `CreateProjectTest.cs` | 6-step Invest-type project wizard, deploy, SDK validation | Existing |
| `FundAndRecoverTest.cs` | Fund-type project: invest above threshold, approve, spend stage, recover | Existing |
| `MultiFundClaimAndRecoverTest.cs` | 3 profiles: Fund project, below/above threshold, claim, penalty recovery | Existing |
| `MultiInvestClaimAndRecoverTest.cs` | 3 profiles: Invest project, stages in past, claim, release, investor claim | Existing |
| `WalletImportAndProjectScanTest.cs` | Import wallet from seed, verify same WalletId, scan projects, verify balance | **NEW** |
| `InvestmentCancellationTest.cs` | 8-phase: cancel before/after approval, re-invest, confirm | **NEW** |

### SDK Unit Tests (Angor.Sdk.Tests)

| Area | Files | Coverage | Status |
|---|---|---|---|
| Founder operations | `FounderAppServiceTests`, `ApproveInvestmentTests`, `CreateProjectTests`, `CreateProjectProfileTests`, `CreateProjectKeysTests`, `SpendStageFundsTests`, `ScanFounderProjectsTests`, `ReleaseFundsTests`, `PublishFounderTransactionTests`, `GetReleasableTransactionsTests`, `GetMoonshotProjectTests`, `GetFounderProjectsTests`, `GetClaimableTransactionsTests` | ~80 tests covering all founder MediatR handlers | **MOSTLY NEW** |
| Project operations | `ProjectAppServiceTests`, `GetProjectRelaysTests`, `ProjectInvestmentsServiceTests` | ~31 tests | **MOSTLY NEW** |
| Investment operations | `InvestmentAppServiceTests`, `CreateInvestmentTests`, `CancelInvestmentRequestTests`, `RequestInvestmentSignaturesTests`, `PublishInvestmentTests`, `PublishAndStoreInvestorTransactionTests`, `NotifyFounderOfInvestmentTests`, `NotifyFounderOfCancellationTests`, `GetTotalInvestedTests`, `GetInvestorNsecTests`, `CheckPenaltyThresholdTests`, `CheckForReleaseSignaturesTests`, `BuildUnfundedReleaseTransactionTests`, `BuildRecoveryTransactionTests`, `BuildPenaltyReleaseTransactionTests`, `BuildEndOfProjectClaimTests` | ~100+ tests covering all investor MediatR handlers | **MOSTLY NEW** |
| Address monitoring | `MonitorAddressForFundsTests`, `AddressPollingServiceTests` | ~26 tests: funds detection, retry, timeout, cancellation | **NEW** |
| Lightning/Boltz | `ClaimLightningSwapTests`, `CreateLightningSwapTests`, `MonitorLightningSwapTests`, `SwapStateExtensionTests`, `BoltzMusig2Tests` | ~37 tests: swap lifecycle, claim, monitoring, BIP-327 key aggregation | **MOSTLY NEW** |
| Database | `LiteDbGenericDocumentCollectionTests` | 3 tests: expression caching bug fix verification | **NEW** |
| Balance | `WalletAccountBalanceServiceTests` | 1 test: stale pending UTXO removal | Existing |

### What Is NOT Covered (Updated)

Areas with zero test coverage have been significantly reduced:

| Area | Gap | Status |
|---|---|---|
| ~~Wallet import (from mnemonic)~~ | ~~Only "Generate" path tested in E2E~~ | **RESOLVED** -- `WalletImportAndProjectScanTest` |
| **Wallet delete** | No test exercises the delete flow | Still missing |
| **Subscribe project type** | Only Invest and Fund types tested E2E | Still missing (unit tests exist for dynamic stages) |
| ~~Investment cancellation E2E~~ | ~~Unit tests only, no end-to-end flow~~ | **RESOLVED** -- `InvestmentCancellationTest` |
| ~~Project scanning after import~~ | ~~ScanFounderProjects never tested E2E~~ | **RESOLVED** -- `WalletImportAndProjectScanTest` |
| **Database state validation** | No test checks LiteDB entries against real LiteDB | Partially addressed (E2E tests now check some DB state; mocks only at SDK level) |
| **Non-zero penalty duration** | MultiFundClaimAndRecoverTest uses PenaltyDays=0 | Still missing |
| **Multiple wallets per profile** | All tests create a single wallet | Still missing |
| **Project discovery/browse** | FindProjects only used as step in invest flows | Still missing |
| ~~Lightning/Boltz swap~~ | ~~Only isolated skipped integration tests~~ | **RESOLVED** -- 17+ unit tests for full swap lifecycle |
| **Database wipe and rebuild** | DeleteAllDataAsync and RebuildAllWalletBalancesAsync untested | Still missing |
| **Network switch** | RebuildAllWalletBalancesAsync re-derivation untested | Still missing |

---

## Database Layer Coverage

### Document Types Stored in LiteDB

There are 8 document collections managed by `IGenericDocumentCollection<T>`:

| Document Type | Key | Description | Tested? |
|---|---|---|---|
| `Project` | `ProjectId` | Local cache for projects fetched from indexer/Nostr | No direct tests |
| `DerivedProjectKeys` | `WalletId` | 15 founder key slots per wallet | Checked in `WalletImportAndProjectScanTest` (E2E) |
| `FounderProjectsDocument` | `WalletId` | Tracks which projects each wallet created | Checked in `WalletImportAndProjectScanTest` (E2E) |
| `InvestmentRecordsDocument` | `WalletId` | Local cache of investment records | Checked in `InvestmentCancellationTest` (E2E) |
| `InvestmentHandshake` | MD5 composite | Investment request/approval handshakes | Checked in `InvestmentCancellationTest` (E2E) |
| `BoltzSwapDocument` | `SwapId` | Lightning submarine swap state | Mocked in swap unit tests |
| `WalletAccountBalanceInfo` | `WalletId` | Cached wallet balance data | Partial (1 unit test + E2E balance checks) |
| `QueryTransaction` | `TransactionId` | Cached transaction data from indexer | No |
| `TransactionHexDocument` | `TransactionId` | Cached raw transaction hex | No |

**Improvement:** The new E2E tests (`WalletImportAndProjectScanTest`, `InvestmentCancellationTest`) now validate some DB state indirectly by checking that data survives through SDK operations. However, no test exercises the `IGenericDocumentCollection<T>` CRUD operations against **real LiteDB** at the SDK unit test level.

### IGenericDocumentCollection Operations Used by Services

| Operation | Used By | Tested? |
|---|---|---|
| `FindByIdAsync(id)` | PortfolioService, DocumentProjectService, WalletAccountBalanceService | Partial (mocks + E2E indirect) |
| `FindByIdsAsync(ids)` | DocumentProjectService | No |
| `InsertAsync(keySelector, items)` | DocumentProjectService | No |
| `UpsertAsync(keySelector, item)` | WalletFactory, PortfolioService, InvestmentHandshakeService | 3 tests (caching bug fix, mock-level) |
| `DeleteAsync(id)` | WalletAccountBalanceService | No |
| `DeleteAllAsync()` | DatabaseManagementService | No |
| `FindAsync(predicate)` | Various | No |
| `CountAsync(predicate)` | Various | No |
| `ExistsAsync(id)` | Various | No |

---

## Data Flow: What Gets Persisted When

### Wallet Creation / Import

| Step | Data Persisted | Storage |
|---|---|---|
| 1. Encrypt wallet | `EncryptedWallet` (AES-256) | `wallets.json` via IStore |
| 2. Init balance | `WalletAccountBalanceInfo` | LiteDB |
| 3. Derive keys | `DerivedProjectKeys` (15 slots) | LiteDB |

### Wallet Deletion

| Step | Data Cleaned | Storage | Gap? |
|---|---|---|---|
| 1. Remove balance | `WalletAccountBalanceInfo` | LiteDB | - |
| 2. Remove from store | `EncryptedWallet` | `wallets.json` | - |
| 3. Clear memory cache | Sensitive data | In-memory | - |
| - | `DerivedProjectKeys` | LiteDB | **NOT CLEANED** (Bug #1 -- still exists) |
| - | `FounderProjectsDocument` | LiteDB | **NOT CLEANED** (Bug #1 -- still exists) |
| - | `InvestmentRecordsDocument` | LiteDB | **NOT CLEANED** (Bug #1 -- still exists) |
| - | `InvestmentHandshake` | LiteDB | **NOT CLEANED** (Bug #1 -- still exists) |

### Project Creation / Deploy

| Step | Data Persisted | Storage |
|---|---|---|
| 1. Nostr profile | Project metadata | Nostr relay |
| 2. Nostr event | ProjectInfo (kind 3030) | Nostr relay |
| 3. On-chain tx | Creation transaction | Bitcoin blockchain |
| 4. Local record | `FounderProjectsDocument` | LiteDB |
| 5. Cache | `Project` | LiteDB |

### Investment Flow

| Step | Data Persisted | Storage |
|---|---|---|
| 1. Request | `InvestmentHandshake` (Status=Pending) | LiteDB + Nostr DM |
| 2. Approval | `InvestmentHandshake` (Status=Approved) | LiteDB + Nostr DM |
| 3. Confirm | `InvestmentHandshake` (Status=Invested) | LiteDB |
| 4. Record | `InvestmentRecordsDocument` | LiteDB + Nostr relay |
| 5. On-chain | Investment transaction | Bitcoin blockchain |

---

## Recommended Priority (Revised)

Given the massive SDK unit test improvements, the priority ranking has shifted. Database-layer and lifecycle tests are now the highest-value additions.

| Priority | Area | Effort | Value | Notes |
|---|---|---|---|---|
| 1 | **Fix Bug #1:** Orphaned DB data on wallet delete | Low | High | Code fix in `WalletAppService.DeleteWallet()` |
| 2 | **Fix Bug #3:** Duplicate wallet guard | Low | High | Code fix in `WalletFactory.CreateWallet()` |
| 3 | LiteDB round-trip tests against real LiteDB (SDK-level, CI-friendly) | Low | High | Still the #1 test gap |
| 4 | DatabaseManagementService tests (SDK-level, CI-friendly) | Low | High | |
| 5 | WalletFactory + WalletAppServiceDelete tests (SDK-level) | Medium | High | Prevents regression on bugs above |
| 6 | Wallet Delete + Reimport (E2E) | Medium | High | Validates full lifecycle |
| 7 | Database Integrity test (E2E) | Medium | High | |
| 8 | Subscribe project type (E2E) | Medium | Medium | |
| 9 | Non-zero PenaltyDays recovery (E2E) | Medium | Medium | |
| 10 | Many Investors scenario (E2E) | High | Medium | |
| 11 | Fix/remove dead `ConfigureMappings()` code | Low | Low | Bug #2 |
| 12 | Expose fee rate selection in investment flow | Low | Medium | Hardcoded to 2 sat/vB |

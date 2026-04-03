# Test Coverage Analysis

## Overview

This document provides a comprehensive analysis of the current test suite across all four test projects, identifies gaps in coverage, and recommends improvements to existing tests and new tests to create.

| Test Project | Tests | Nature |
|---|---|---|
| `App.Test.Integration` (src/design/) | 7 | E2E headless UI tests (real testnet) |
| `Angor.Sdk.Tests` (src/sdk/) | ~121 | SDK unit tests + skipped integration tests |
| `Angor.Shared.Tests` (src/shared/) | ~114 | Bitcoin protocol & domain model unit tests |
| `AngorApp.Tests` (src/avalonia/) | 52 | ViewModel & UI logic unit tests |
| **Total** | **~294** | |

## Related Documents

- [Improvements to Existing Tests](TEST_IMPROVEMENTS.md)
- [New Test Proposals](TEST_NEW_PROPOSALS.md)
- [Edge Cases and Known Issues](TEST_EDGE_CASES.md)
- [SDK-Level Test Proposals](TEST_SDK_PROPOSALS.md)

---

## Current Coverage by Area

### E2E Integration Tests (App.Test.Integration)

| Test File | What It Covers |
|---|---|
| `SmokeTest.cs` | Headless platform boot, AutomationId lookup |
| `SendToSelfTest.cs` | Wallet create, faucet fund, send to self, verify balance |
| `CreateProjectTest.cs` | 6-step Invest-type project wizard, deploy, SDK validation |
| `FundAndRecoverTest.cs` | Fund-type project: invest above threshold, approve, spend stage, recover |
| `MultiFundClaimAndRecoverTest.cs` | 3 profiles: Fund project, below/above threshold, claim, penalty recovery |
| `MultiInvestClaimAndRecoverTest.cs` | 3 profiles: Invest project, stages in past, claim, release, investor claim |

### SDK Unit Tests (Angor.Sdk.Tests)

| Area | Files | Coverage |
|---|---|---|
| Founder operations | `FounderAppServiceTests` | GetProjectInvestments, CreateProjectInfo validation |
| Project operations | `ProjectAppServiceTests` | LatestProjects, GetProject, TryGetProject, ProjectStats |
| Investment operations | `InvestmentAppServiceTests`, `CreateInvestmentTests`, `CancelInvestmentRequestTests` | GetInvestments, GetRecoveryStatus, GetPenalties, BuildDraft, Cancel |
| Address monitoring | `MonitorAddressForFundsTests`, `AddressPollingServiceTests` | Funds detection, retry, timeout, cancellation |
| Lightning/Boltz | `LightningSwapTests`, `BoltzMusig2Tests` | Swap lifecycle, BIP-327 key aggregation |
| Database | `LiteDbGenericDocumentCollectionTests` | Expression caching bug fix |
| Balance | `WalletAccountBalanceServiceTests` | Stale pending UTXO removal |

### What Is NOT Covered

The following areas have **zero test coverage**:

| Area | Gap |
|---|---|
| **Wallet import** (from mnemonic) | Only "Generate" path tested in E2E |
| **Wallet delete** | No test exercises the delete flow |
| **Subscribe project type** | Only Invest and Fund types tested |
| **Investment cancellation E2E** | Unit tests only, no end-to-end flow |
| **Project scanning after import** | ScanFounderProjects never tested E2E |
| **Database state validation** | No test checks LiteDB entries directly |
| **Non-zero penalty duration** | MultiFundClaimAndRecoverTest uses PenaltyDays=0 |
| **Multiple wallets per profile** | All tests create a single wallet |
| **Project discovery/browse** | FindProjects only used as step in invest flows |
| **Lightning/Boltz swap E2E** | Only isolated skipped integration tests |
| **Database wipe and rebuild** | DeleteAllDataAsync and RebuildAllWalletBalancesAsync untested |
| **Network switch** | RebuildAllWalletBalancesAsync re-derivation untested |

---

## Database Layer Coverage

### Document Types Stored in LiteDB

There are 8 document collections managed by `IGenericDocumentCollection<T>`:

| Document Type | Key | Description | Tested? |
|---|---|---|---|
| `Project` | `ProjectId` | Local cache for projects fetched from indexer/Nostr | No |
| `DerivedProjectKeys` | `WalletId` | 15 founder key slots per wallet | No |
| `FounderProjectsDocument` | `WalletId` | Tracks which projects each wallet created | No |
| `InvestmentRecordsDocument` | `WalletId` | Local cache of investment records | No |
| `InvestmentHandshake` | MD5 composite | Investment request/approval handshakes | No |
| `BoltzSwapDocument` | `SwapId` | Lightning submarine swap state | No |
| `WalletAccountBalanceInfo` | `WalletId` | Cached wallet balance data | Partial (1 test) |
| `QueryTransaction` | `TransactionId` | Cached transaction data from indexer | No |
| `TransactionHexDocument` | `TransactionId` | Cached raw transaction hex | No |

### IGenericDocumentCollection Operations Used by Services

| Operation | Used By | Tested? |
|---|---|---|
| `FindByIdAsync(id)` | PortfolioService, DocumentProjectService, WalletAccountBalanceService | Partial |
| `FindByIdsAsync(ids)` | DocumentProjectService | No |
| `InsertAsync(keySelector, items)` | DocumentProjectService | No |
| `UpsertAsync(keySelector, item)` | WalletFactory, PortfolioService, InvestmentHandshakeService | 1 test (caching bug) |
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
| - | `DerivedProjectKeys` | LiteDB | **NOT CLEANED** |
| - | `FounderProjectsDocument` | LiteDB | **NOT CLEANED** |
| - | `InvestmentRecordsDocument` | LiteDB | **NOT CLEANED** |

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

## Recommended Priority

| Priority | Area | Effort | Value |
|---|---|---|---|
| 1 | LiteDB round-trip tests (SDK-level, CI-friendly) | Low | High |
| 2 | WalletFactory integration tests (SDK-level, CI-friendly) | Low | High |
| 3 | Wallet Import + Project Scan (E2E) | Medium | High |
| 4 | Wallet Delete + Reimport (E2E) | Medium | High |
| 5 | Database Integrity test (E2E) | Medium | High |
| 6 | Many Investors scenario (E2E) | High | High |
| 7 | Strengthen existing tests with DB assertions | Medium | Medium |
| 8 | Investment cancellation, subscribe type, edge cases | Medium | Medium |

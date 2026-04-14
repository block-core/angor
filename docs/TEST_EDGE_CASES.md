# Edge Cases and Known Issues

> **Last updated:** April 2026 -- re-analyzed after significant test additions.

This document catalogs edge cases that should be tested and issues discovered during the test coverage analysis.

---

## Known Issues (Discovered During Analysis)

### Issue 1: DerivedProjectKeys Not Cleaned on Wallet Delete -- STILL EXISTS

**Location:** `WalletAppService.DeleteWallet()` in `src/sdk/Angor.Sdk/Wallet/Infrastructure/Impl/WalletAppService.cs` (lines 233-264)

**Description:** When a wallet is deleted, the `EncryptedWallet` and `WalletAccountBalanceInfo` are removed, but `DerivedProjectKeys`, `FounderProjectsDocument`, `InvestmentRecordsDocument`, and `InvestmentHandshake` are left orphaned in LiteDB.

**Impact:** Disk space leak and potential data confusion if a different wallet is later created and happens to collide (unlikely but possible).

**What gets cleaned:**
| Data | Cleaned? |
|---|---|
| `EncryptedWallet` in `wallets.json` | Yes |
| `WalletAccountBalanceInfo` in LiteDB | Yes |
| In-memory sensitive data cache | Yes |
| `DerivedProjectKeys` in LiteDB | **No** |
| `FounderProjectsDocument` in LiteDB | **No** |
| `InvestmentRecordsDocument` in LiteDB | **No** |
| `InvestmentHandshake` records in LiteDB | **No** |

**Recommended fix:** Inject the 4 missing `IGenericDocumentCollection<T>` instances into `WalletAppService` and add deletion by `WalletId` in the delete flow. The `WalletAppService` constructor (lines 13-21) currently does not have these collections as dependencies.

**Test coverage:** None. Proposed in `WalletAppServiceDeleteTests` (see TEST_SDK_PROPOSALS.md section 6).

---

### Issue 2: LiteDbDocumentMapping.ConfigureMappings() is Dead Code -- STILL EXISTS

**Location:** `src/sdk/Angor.Data.Documents.LiteDb/LiteDbDocumentMapping.cs` (line 6)

**Description:** `ConfigureMappings()` is defined but never called anywhere in the codebase (zero call sites found by grep). LiteDB uses its default conventions (which happen to map `Id` to `_id` automatically), so things work by accident.

**Impact:** Low risk currently, but fragile. If a document type is added with non-standard field names, the explicit mapping won't apply.

**Recommended fix:** Either call `ConfigureMappings()` during DI setup or remove the dead code.

---

### Issue 3: No Duplicate Wallet Guard -- STILL EXISTS

**Location:** `WalletFactory.CreateWallet()` in `src/sdk/Angor.Sdk/Wallet/Infrastructure/Impl/WalletFactory.cs` (lines 23-57, `SaveEncryptedWalletToStoreAsync` at lines 59-68)

**Description:** There is no check for whether a wallet with the same `WalletId` (derived from xpub hash) already exists. Importing the same seed words twice will:
1. Append a second `EncryptedWallet` entry to `wallets.json` with the same `Id`.
2. Upsert (overwrite) `WalletAccountBalanceInfo` in LiteDB.
3. Upsert (overwrite) `DerivedProjectKeys` in LiteDB.

**Impact:** The duplicate entry in `wallets.json` could cause issues when loading wallets (two entries with the same ID). The LiteDB upserts are harmless but wasteful.

**Recommended fix:** Check `walletStore.GetAll()` for an existing wallet with the same `Id` before creating. Return `Result.Failure("Wallet already exists")` if found.

**Test coverage:** None. Proposed in `WalletFactoryIntegrationTests` (see TEST_SDK_PROPOSALS.md section 2) and `DuplicateWalletImportTest` E2E (see TEST_NEW_PROPOSALS.md section 8).

---

### Issue 4: FounderProjectsDocument Not Cleaned on Wallet Delete -- STILL EXISTS

Same root cause as Issue 1. `FounderProjectsDocument` tracks which projects a wallet created but is not cleaned up on wallet deletion.

---

## Edge Cases to Test

### Wallet Operations

| Edge Case | Expected Behavior | Risk | Test Coverage |
|---|---|---|---|
| Import wallet with 24-word mnemonic | Should work (only 12-word tested currently) | Medium - different derivation | None |
| Import wallet with BIP-39 passphrase | Different `WalletId` from same seed (passphrase changes master key) | High - totally untested path | None |
| Import same seed words twice | Should reject duplicate or handle gracefully | High - known Issue 3 | None |
| Delete wallet with active investments | Should warn or block (currently doesn't) | High - investor loses recovery ability | None |
| Create wallet with empty encryption key | Should fail validation | Medium | None |
| Wrong encryption key during decrypt | Should return `Result.Failure`, not crash | Medium | None |
| Import with invalid/misspelled seed words | UI validates BIP-39 words, SDK throws `DomainException` | Low - validation exists | None |

### Project Operations

| Edge Case | Expected Behavior | Risk | Test Coverage |
|---|---|---|---|
| Create project with all 15 key slots used | Should fail gracefully when no slots available | High - no guard exists | None |
| Create project with 0 stages | Should be rejected by validation | Medium | None |
| Create project with stages summing to != 100% | SDK validation exists (`CreateProjectInfo` checks), needs E2E confirmation | Low | None |
| Invest in own project (founder invests in own project) | Should be blocked or warned | Medium - unclear if guarded | None |
| Invest with exactly threshold amount | Off-by-one: is threshold "above" or "at-or-above"? | Medium | Partially covered: `InvestmentCancellationTest` tests above-threshold, `MultiFundClaimAndRecoverTest` tests below-threshold |
| Invest with zero amount | Should be rejected | Low | None |
| Invest when project has expired | Should be rejected | Medium | None |
| Invest when project hasn't started yet | Should be rejected | Medium | None |

### Database Operations

| Edge Case | Expected Behavior | Risk | Test Coverage |
|---|---|---|---|
| `DeleteAllDataAsync` during active wallet operations | Should not corrupt LiteDB (thread safety) | Medium - LiteDB is single-writer | None |
| `FindByIdAsync` with null/empty ID | Should return `Result.Failure` | Low | None |
| `UpsertAsync` with null entity | Should return `Result.Failure` | Low | None |
| `InsertAsync` with duplicate key | LiteDB throws; should be caught and returned as `Result.Failure` | Medium | None |
| `FindAsync` with complex predicate | Expression rewriting in `LiteDbGenericDocumentCollection` might fail for complex lambdas | Medium | Partially: `LiteDbGenericDocumentCollectionTests` validates expression compilation |
| LiteDB file corruption recovery | What happens if `.db` file is corrupted? | High - no recovery mechanism | None |
| Concurrent writes to same document | LiteDB is single-writer but SDK services might queue writes | Low | None |

### Network / Infrastructure Edge Cases

| Edge Case | Expected Behavior | Risk | Test Coverage |
|---|---|---|---|
| Indexer down during project scan | `ScanFounderProjects` should fail gracefully with `Result.Failure` | Medium | **Covered** -- `ScanFounderProjectsTests.Handle_WhenScanFailsButLocalProjectsExist_StillReturnsLocalProjects` |
| Nostr relay timeout during handshake | Partial `InvestmentHandshake` state in DB (Pending but never Approved) | High | None |
| Indexer stale data after broadcast | Optimistic local updates should prevent stale reads | Medium - documented in AGENTS.md | None |
| Network switch mid-investment | Derived keys change, handshake breaks, funds at risk | High | None |
| Multiple concurrent investments to same project | Race conditions in handshake sync on Nostr | Medium | None |
| Relay returns duplicate events | `DocumentProjectService` deduplicates by identifier, but handshake service might not | Medium | None |

### Multi-Profile / Multi-Wallet Edge Cases

| Edge Case | Expected Behavior | Risk | Test Coverage |
|---|---|---|---|
| Two wallets in same profile | Balance and project isolation between wallets | Medium - untested | None |
| Two profiles with same wallet (imported separately) | Should work independently (separate LiteDB files) | Low | **Covered** -- `WalletImportAndProjectScanTest` validates same wallet in two profiles |
| Profile name with special characters | `SanitizeProfileName` replaces invalid chars with `-` | Low - sanitization exists | None |
| Very long profile name | Should be handled by filesystem limits | Low | None |

---

## Edge Cases by Priority (Revised)

### Must Test (High Risk, No Coverage)

1. **Import same seed words twice** (duplicate wallet guard missing -- Bug #3)
2. **Delete wallet with active investments** (no warning, data orphaned -- Bug #1)
3. **Create project when all 15 key slots used** (no guard)
4. **Network switch mid-investment** (derived keys change)
5. **Nostr relay timeout during handshake** (partial state)

### Should Test (Medium Risk)

6. **Import with 24-word mnemonic** (different from tested 12-word)
7. **Import with BIP-39 passphrase** (different WalletId)
8. **Invest at exactly threshold amount** (off-by-one)
9. **Invest in own project** (unclear if blocked)
10. ~~Indexer down during project scan~~ **COVERED** by `ScanFounderProjectsTests`
11. **LiteDB InsertAsync with duplicate key** (error propagation)
12. **`DeleteAllDataAsync` during active operations** (thread safety)

### Nice to Test (Low Risk)

13. **Invalid seed words at UI level** (validation exists)
14. **Empty/null IDs in DB operations** (basic error handling)
15. **Profile name sanitization** (exists, just needs confirmation)

---

## Progress Since Original Analysis

| Category | Original Edge Cases Without Coverage | Current |
|---|---|---|
| Network/Infrastructure | 6 | **5** (indexer down now covered) |
| Multi-Profile | 4 | **3** (same wallet in two profiles now covered) |
| Wallet Operations | 7 | 7 (no change) |
| Project Operations | 8 | 8 (no change, threshold partially covered) |
| Database Operations | 7 | 7 (expression rewriting partially covered) |

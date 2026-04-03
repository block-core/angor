# Edge Cases and Known Issues

This document catalogs edge cases that should be tested and issues discovered during the test coverage analysis.

---

## Known Issues (Discovered During Analysis)

### Issue 1: DerivedProjectKeys Not Cleaned on Wallet Delete

**Location:** `WalletAppService.DeleteWallet()` in `src/sdk/Angor.Sdk/Wallet/Infrastructure/Impl/WalletAppService.cs`

**Description:** When a wallet is deleted, the `EncryptedWallet` and `WalletAccountBalanceInfo` are removed, but `DerivedProjectKeys`, `FounderProjectsDocument`, and `InvestmentRecordsDocument` are left orphaned in LiteDB.

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

**Recommended fix:** Add deletion of `DerivedProjectKeys`, `FounderProjectsDocument`, and `InvestmentRecordsDocument` by `WalletId` in the delete flow.

---

### Issue 2: LiteDbDocumentMapping.ConfigureMappings() is Dead Code

**Location:** `src/sdk/Angor.Data.Documents.LiteDb/LiteDbDocumentMapping.cs`

**Description:** `ConfigureMappings()` is defined but never called anywhere in the codebase. LiteDB uses its default conventions (which happen to map `Id` to `_id` automatically), so things work by accident.

**Impact:** Low risk currently, but fragile. If a document type is added with non-standard field names, the explicit mapping won't apply.

---

### Issue 3: No Duplicate Wallet Guard

**Location:** `WalletFactory.CreateWallet()` in `src/sdk/Angor.Sdk/Wallet/Infrastructure/Impl/WalletFactory.cs`

**Description:** There is no check for whether a wallet with the same `WalletId` (derived from xpub hash) already exists. Importing the same seed words twice will:
1. Append a second `EncryptedWallet` entry to `wallets.json` with the same `Id`.
2. Upsert (overwrite) `WalletAccountBalanceInfo` in LiteDB.
3. Upsert (overwrite) `DerivedProjectKeys` in LiteDB.

**Impact:** The duplicate entry in `wallets.json` could cause issues when loading wallets (two entries with the same ID). The LiteDB upserts are harmless but wasteful.

**Recommended fix:** Check `walletStore.GetAll()` for an existing wallet with the same `Id` before creating. Return `Result.Failure("Wallet already exists")` if found.

---

### Issue 4: FounderProjectsDocument Not Cleaned on Wallet Delete

Same root cause as Issue 1. `FounderProjectsDocument` tracks which projects a wallet created but is not cleaned up on wallet deletion.

---

## Edge Cases to Test

### Wallet Operations

| Edge Case | Expected Behavior | Risk |
|---|---|---|
| Import wallet with 24-word mnemonic | Should work (only 12-word tested currently) | Medium - different derivation |
| Import wallet with BIP-39 passphrase | Different `WalletId` from same seed (passphrase changes master key) | High - totally untested path |
| Import same seed words twice | Should reject duplicate or handle gracefully | High - known Issue 3 |
| Delete wallet with active investments | Should warn or block (currently doesn't) | High - investor loses recovery ability |
| Create wallet with empty encryption key | Should fail validation | Medium |
| Wrong encryption key during decrypt | Should return `Result.Failure`, not crash | Medium |
| Import with invalid/misspelled seed words | UI validates BIP-39 words, SDK throws `DomainException` | Low - validation exists |

### Project Operations

| Edge Case | Expected Behavior | Risk |
|---|---|---|
| Create project with all 15 key slots used | Should fail gracefully when no slots available | High - no guard exists |
| Create project with 0 stages | Should be rejected by validation | Medium |
| Create project with stages summing to != 100% | SDK validation exists (`CreateProjectInfo` checks), needs E2E confirmation | Low |
| Invest in own project (founder invests in own project) | Should be blocked or warned | Medium - unclear if guarded |
| Invest with exactly threshold amount | Off-by-one: is threshold "above" or "at-or-above"? | Medium |
| Invest with zero amount | Should be rejected | Low |
| Invest when project has expired | Should be rejected | Medium |
| Invest when project hasn't started yet | Should be rejected | Medium |

### Database Operations

| Edge Case | Expected Behavior | Risk |
|---|---|---|
| `DeleteAllDataAsync` during active wallet operations | Should not corrupt LiteDB (thread safety) | Medium - LiteDB is single-writer |
| `FindByIdAsync` with null/empty ID | Should return `Result.Failure` | Low |
| `UpsertAsync` with null entity | Should return `Result.Failure` | Low |
| `InsertAsync` with duplicate key | LiteDB throws; should be caught and returned as `Result.Failure` | Medium |
| `FindAsync` with complex predicate | Expression rewriting in `LiteDbGenericDocumentCollection` might fail for complex lambdas | Medium |
| LiteDB file corruption recovery | What happens if `.db` file is corrupted? | High - no recovery mechanism |
| Concurrent writes to same document | LiteDB is single-writer but SDK services might queue writes | Low |

### Network / Infrastructure Edge Cases

| Edge Case | Expected Behavior | Risk |
|---|---|---|
| Indexer down during project scan | `ScanFounderProjects` should fail gracefully with `Result.Failure` | Medium |
| Nostr relay timeout during handshake | Partial `InvestmentHandshake` state in DB (Pending but never Approved) | High |
| Indexer stale data after broadcast | Optimistic local updates should prevent stale reads | Medium - documented in AGENTS.md |
| Network switch mid-investment | Derived keys change, handshake breaks, funds at risk | High |
| Multiple concurrent investments to same project | Race conditions in handshake sync on Nostr | Medium |
| Relay returns duplicate events | `DocumentProjectService` deduplicates by identifier, but handshake service might not | Medium |

### Multi-Profile / Multi-Wallet Edge Cases

| Edge Case | Expected Behavior | Risk |
|---|---|---|
| Two wallets in same profile | Balance and project isolation between wallets | Medium - untested |
| Two profiles with same wallet (imported separately) | Should work independently (separate LiteDB files) | Low |
| Profile name with special characters | `SanitizeProfileName` replaces invalid chars with `-` | Low - sanitization exists |
| Very long profile name | Should be handled by filesystem limits | Low |

---

## Edge Cases by Priority

### Must Test (High Risk, No Coverage)

1. **Import same seed words twice** (duplicate wallet guard missing)
2. **Delete wallet with active investments** (no warning, data orphaned)
3. **Create project when all 15 key slots used** (no guard)
4. **Network switch mid-investment** (derived keys change)
5. **Nostr relay timeout during handshake** (partial state)

### Should Test (Medium Risk)

6. **Import with 24-word mnemonic** (different from tested 12-word)
7. **Import with BIP-39 passphrase** (different WalletId)
8. **Invest at exactly threshold amount** (off-by-one)
9. **Invest in own project** (unclear if blocked)
10. **Indexer down during project scan** (error handling)
11. **LiteDB InsertAsync with duplicate key** (error propagation)
12. **`DeleteAllDataAsync` during active operations** (thread safety)

### Nice to Test (Low Risk)

13. **Invalid seed words at UI level** (validation exists)
14. **Empty/null IDs in DB operations** (basic error handling)
15. **Profile name sanitization** (exists, just needs confirmation)
